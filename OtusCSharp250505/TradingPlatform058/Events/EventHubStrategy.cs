// Events/EventHubStrategy.cs

// Events/EventHubStrategy.cs

using GS.Trade.Strategies;
using GS.Trade.Abstractions;
using BusLibrary02.Core;
using TradingPlatform.Events;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace TradingPlatform.Services
{
    public class EventHubStrategy : EventDrivenRandomStrategy01
    {
        private readonly IEventHub _eventHub;
        private readonly ILogger<EventHubStrategy>? _logger;

        // Для дедупликации позиций
        private readonly object _positionLock = new();
        private (string Ticker, string Strategy, int NetQuantity) _lastPosition;
        private DateTime _lastPositionEventTime = DateTime.MinValue;
        private readonly TimeSpan _positionEventCooldown = TimeSpan.FromSeconds(1);

        // Используем Reflection для доступа к приватному полю _currentAction
        private TradingAction GetCurrentAction()
        {
            var field = typeof(EventDrivenRandomStrategy01).GetField("_currentAction",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (TradingAction)field?.GetValue(this)!;
        }

        private bool GetIsRunning()
        {
            var field = typeof(EventDrivenRandomStrategy01).GetField("_isRunning",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (bool)field?.GetValue(this)!;
        }

        public EventHubStrategy(string ticker, int cycleTicks, string strategyName,
            IEventHub eventHub, ILogger<EventHubStrategy>? logger = null)
            : base(ticker, cycleTicks, strategyName)
        {
            _eventHub = eventHub;
            _logger = logger;

            _logger?.LogInformation("🔄 Создана EventHubStrategy: {Name} для {Ticker}",
                strategyName, ticker);

            // Проверим, доступно ли свойство StrategyName
            _logger?.LogDebug("Свойство StrategyName: {StrategyName}", this.StrategyName);

            // Подписываемся на события стратегии и публикуем их через EventHub
            this.OnTradeExecuted += OnTradeExecutedHandler;
            this.OnDealClosed += OnDealClosedHandler;
            this.OnPositionChanged += OnPositionChangedHandler;
            this.OnOrderCreated += OnOrderCreatedHandler;

            _logger?.LogDebug("Подписаны на {Count} событий стратегии", 4); // 4 обработчика

            _logger?.LogDebug("Создана стратегия {Name} для {Ticker}", strategyName, ticker);
        }

        public new void StartTrading(CancellationToken cancellationToken = default)
        {
            base.StartTrading(cancellationToken);

            _logger?.LogInformation("Стратегия {Name} для {Ticker} запущена",
                StrategyName, Ticker);

            // Публикуем событие о запуске стратегии
            _ = _eventHub.PublishAsync(new SystemStatusEvent(
                "Strategy",
                "Started",
                $"Стратегия {StrategyName} для {Ticker} запущена",
                DateTime.Now
            ));
        }

        public new void StopTrading()
        {
            base.StopTrading();

            _logger?.LogInformation("Стратегия {Name} для {Ticker} остановлена",
                StrategyName, Ticker);

            // Публикуем событие об остановке стратегии
            _ = _eventHub.PublishAsync(new SystemStatusEvent(
                "Strategy",
                "Stopped",
                $"Стратегия {StrategyName} для {Ticker} остановлена",
                DateTime.Now
            ));
        }

        // Добавляем публичный метод IsRunning для проверки статуса стратегии
        public bool IsRunning() => GetIsRunning();

        // Добавляем метод для получения периода стратегии
        public int GetCycleTicks()
        {
            // Получаем приватное поле _cycleTicks через рефлексию
            var field = typeof(EventDrivenRandomStrategy01).GetField("_cycleTicks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? (int)field.GetValue(this)! : 10;
        }

        // EventHubStrategy.cs - изменить логирование
        private async void OnTradeExecutedHandler(ITrade trade)
        {
            if (!IsRunning()) return;

            try
            {
                var tradeEvent = new TradeExecutedEvent(
                    trade.Ticker,
                    trade.Side.ToString(),
                    trade.Price,
                    trade.Qty,
                    StrategyName,
                    trade.DateTime
                );

                await _eventHub.PublishAsync(tradeEvent);

                // Важные действия логируем как Information
                _logger?.LogInformation("📊 TRADE: {Ticker} {Side} {Qty} @ {Price} ({Strategy})",
                    trade.Ticker, trade.Side, trade.Qty, trade.Price, StrategyName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка при публикации события TradeExecuted");
            }
        }

        private async void OnDealClosedHandler(IDeal deal)
        {
            if (!IsRunning()) return;

            try
            {
                var strategyName = this.StrategyName;

                var dealEvent = new DealClosedEvent(
                    deal.Ticker,
                    strategyName,
                    deal.Number,
                    deal.Side.ToString(),
                    deal.Qty,
                    deal.OpenPrice,
                    deal.ClosePrice,
                    deal.PnL,
                    deal.DateTime
                );

                await _eventHub.PublishAsync(dealEvent);

                _logger?.LogInformation("💰 DEAL CLOSED: {Ticker} {Strategy}: PnL={PnL:F2}",
                    deal.Ticker, strategyName, deal.PnL);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка при публикации события DealClosed");
            }
        }

        private async void OnPositionChangedHandler(IPositionSummary position)
        {
            if (!IsRunning()) return;

            try
            {
                var now = DateTime.Now;

                // Проверяем кулдаун - не публикуем события позиции чаще чем раз в секунду
                if (now - _lastPositionEventTime < _positionEventCooldown)
                {
                    _logger?.LogDebug("Кулдаун позиции для {Ticker} {Strategy}", position.Ticker, StrategyName);
                    return;
                }

                // Проверяем, изменилась ли позиция (сравниваем количество, а не P&L)
                bool positionChanged = false;

                lock (_positionLock)
                {
                    if (_lastPosition.Ticker != position.Ticker ||
                        _lastPosition.Strategy != StrategyName ||
                        _lastPosition.NetQuantity != position.NetQuantity)
                    {
                        positionChanged = true;
                        _lastPosition = (position.Ticker, StrategyName, position.NetQuantity);
                        _lastPositionEventTime = now;
                    }
                }

                // Публикуем событие только если позиция изменилась (количество, а не P&L)
                if (positionChanged)
                {
                    var positionEvent = new PositionChangedEvent(
                        position.Ticker,
                        StrategyName,
                        position.NetQuantity,
                        position.UnrealizedProfit,
                        position.Status.ToString(),
                        now
                    );

                    await _eventHub.PublishAsync(positionEvent);

                    // Важные позиции - как Information
                    _logger?.LogInformation("📈 POSITION CHANGED: {Ticker} {Strategy}: NetQty={NetQty} | P&L={PnL:F2}",
                        position.Ticker, StrategyName, position.NetQuantity, position.UnrealizedProfit);
                }
                else
                {
                    // Только P&L изменился, не публикуем событие
                    _logger?.LogTrace("P&L update (no position change): {Ticker} {Strategy}: NetQty={NetQty} | P&L={PnL:F2}",
                        position.Ticker, StrategyName, position.NetQuantity, position.UnrealizedProfit);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка при публикации события PositionChanged");
            }
        }

        private async void OnOrderCreatedHandler(IOrder order)
        {
            if (!IsRunning()) return;

            try
            {
                var orderEvent = new OrderCreatedEvent(
                    order.Ticker,
                    order.Side.ToString(),
                    order.Price,
                    order.Qty,
                    order.Status.ToString(),
                    order.OrderNumber.ToString(),
                    order.DateTime
                );

                await _eventHub.PublishAsync(orderEvent);
                _logger?.LogDebug("✅ Опубликовано событие OrderCreated: {Ticker} {Side}",
                    order.Ticker, order.Side);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка при публикации события OrderCreated");
            }
        }

        public override void ProcessTick(ITick tick)
        {
            if (!IsRunning()) return;

            try
            {
                base.ProcessTick(tick);

                // Публикуем сигнал стратегии
                var currentAction = GetCurrentAction();
                var signalEvent = new StrategySignalEvent(
                    tick.Ticker,
                    StrategyName,
                    currentAction.ToString(),
                    tick.Price,
                    1,
                    tick.DateTime
                );

                _ = _eventHub.PublishAsync(signalEvent);

                // _logger?.LogTrace("Стратегия {Name} обработала тик: {Ticker} {Price}",
                //     StrategyName, tick.Ticker, tick.Price);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка в ProcessTick стратегии {Name}", StrategyName);
            }
        }

        // Метод для сброса состояния дедупликации (опционально)
        public void ResetDeduplication()
        {
            lock (_positionLock)
            {
                _lastPosition = default;
                _lastPositionEventTime = DateTime.MinValue;
                _logger?.LogDebug("Сброшено состояние дедупликации для стратегии {Strategy}", StrategyName);
            }
        }
    }
}

//using GS.Trade.Strategies;
//using GS.Trade.Abstractions;
//using BusLibrary02.Core;
//using TradingPlatform.Events;
//using Microsoft.Extensions.Logging;
//using System.Reflection;

//namespace TradingPlatform.Services
//{
//    public class EventHubStrategy : EventDrivenRandomStrategy01
//    {
//        private readonly IEventHub _eventHub;
//        private readonly ILogger<EventHubStrategy>? _logger;

//        // Используем Reflection для доступа к приватному полю _currentAction
//        private TradingAction GetCurrentAction()
//        {
//            var field = typeof(EventDrivenRandomStrategy01).GetField("_currentAction",
//                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//            return (TradingAction)field?.GetValue(this)!;
//        }

//        private bool GetIsRunning()
//        {
//            var field = typeof(EventDrivenRandomStrategy01).GetField("_isRunning",
//                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//            return (bool)field?.GetValue(this)!;
//        }

//        public EventHubStrategy(string ticker, int cycleTicks, string strategyName,
//            IEventHub eventHub, ILogger<EventHubStrategy>? logger = null)
//            : base(ticker, cycleTicks, strategyName)
//        {
//            _eventHub = eventHub;
//            _logger = logger;

//            _logger?.LogInformation("🔄 Создана EventHubStrategy: {Name} для {Ticker}",
//                strategyName, ticker);

//            // Проверим, доступно ли свойство StrategyName
//            _logger?.LogDebug("Свойство StrategyName: {StrategyName}", this.StrategyName);

//            // Подписываемся на события стратегии и публикуем их через EventHub
//            this.OnTradeExecuted += OnTradeExecutedHandler;
//            this.OnDealClosed += OnDealClosedHandler;
//            this.OnPositionChanged += OnPositionChangedHandler;
//            this.OnOrderCreated += OnOrderCreatedHandler;

//            _logger?.LogDebug("Подписаны на {Count} событий стратегии",4); // 4 обработчика

//            _logger?.LogDebug("Создана стратегия {Name} для {Ticker}", strategyName, ticker);
//        }

//        public new void StartTrading(CancellationToken cancellationToken = default)
//        {
//            base.StartTrading(cancellationToken);

//            _logger?.LogInformation("Стратегия {Name} для {Ticker} запущена",
//                StrategyName, Ticker);

//            // Публикуем событие о запуске стратегии
//            _ = _eventHub.PublishAsync(new SystemStatusEvent(
//                "Strategy",
//                "Started",
//                $"Стратегия {StrategyName} для {Ticker} запущена",
//                DateTime.Now
//            ));
//        }

//        public new void StopTrading()
//        {
//            base.StopTrading();

//            _logger?.LogInformation("Стратегия {Name} для {Ticker} остановлена",
//                StrategyName, Ticker);

//            // Публикуем событие об остановке стратегии
//            _ = _eventHub.PublishAsync(new SystemStatusEvent(
//                "Strategy",
//                "Stopped",
//                $"Стратегия {StrategyName} для {Ticker} остановлена",
//                DateTime.Now
//            ));
//        }

//        // Добавляем публичный метод IsRunning для проверки статуса стратегии
//        public bool IsRunning() => GetIsRunning();

//        // Добавляем метод для получения периода стратегии
//        public int GetCycleTicks()
//        {
//            // Получаем приватное поле _cycleTicks через рефлексию
//            var field = typeof(EventDrivenRandomStrategy01).GetField("_cycleTicks",
//                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//            return field != null ? (int)field.GetValue(this)! : 10;
//        }

//        // EventHubStrategy.cs - изменить логирование
//        private async void OnTradeExecutedHandler(ITrade trade)
//        {
//            if (!IsRunning()) return;

//            try
//            {
//                var tradeEvent = new TradeExecutedEvent(
//                    trade.Ticker,
//                    trade.Side.ToString(),
//                    trade.Price,
//                    trade.Qty,
//                    StrategyName,
//                    trade.DateTime
//                );

//                await _eventHub.PublishAsync(tradeEvent);

//                // Важные действия логируем как Information
//                _logger?.LogInformation("📊 TRADE: {Ticker} {Side} {Qty} @ {Price} ({Strategy})",
//                    trade.Ticker, trade.Side, trade.Qty, trade.Price, StrategyName);
//            }
//            catch (Exception ex)
//            {
//                _logger?.LogError(ex, "❌ Ошибка при публикации события TradeExecuted");
//            }
//        }


//        private async void OnDealClosedHandler(IDeal deal)
//        {
//            if (!IsRunning()) return;

//            try
//            {
//                var strategyName = this.StrategyName;

//                var dealEvent = new DealClosedEvent(
//                    deal.Ticker,
//                    strategyName,
//                    deal.Number,
//                    deal.Side.ToString(),
//                    deal.Qty,
//                    deal.OpenPrice,
//                    deal.ClosePrice,
//                    deal.PnL,
//                    deal.DateTime
//                );

//                await _eventHub.PublishAsync(dealEvent);
//                //_logger?.LogDebug("✅ Опубликовано событие DealClosed: {Ticker} PnL={PnL}",
//                //    deal.Ticker,  deal.PnL, strategyName);
//                _logger?.LogInformation("💰 DEAL CLOSED: {Ticker} {Strategy}: PnL={PnL:F2}",
//                            deal.Ticker, strategyName, deal.PnL);
//            }
//            catch (Exception ex)
//            {
//                _logger?.LogError(ex, "❌ Ошибка при публикации события DealClosed");
//            }
//        }

//        private async void OnPositionChangedHandler(IPositionSummary position)
//        {
//            if (!IsRunning()) return;

//            try
//            {
//                var positionEvent = new PositionChangedEvent(
//                    position.Ticker,
//                    StrategyName,
//                    position.NetQuantity,
//                    position.UnrealizedProfit,
//                    position.Status.ToString(),
//                    DateTime.Now
//                );

//                await _eventHub.PublishAsync(positionEvent);

//                // Важные позиции - как Information
//                if (Math.Abs(position.NetQuantity) > 0)
//                {
//                    _logger?.LogInformation("📈 POSITION: {Ticker} {Strategy}: NetQty={NetQty} | P&L={PnL:F2}",
//                        position.Ticker, StrategyName, position.NetQuantity, position.UnrealizedProfit);
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger?.LogError(ex, "❌ Ошибка при публикации события PositionChanged");
//            }
//        }



//        private async void OnOrderCreatedHandler(IOrder order)
//        {
//            if (!IsRunning()) return;

//            try
//            {
//                var orderEvent = new OrderCreatedEvent(
//                    order.Ticker,
//                    order.Side.ToString(),
//                    order.Price,
//                    order.Qty,
//                    order.Status.ToString(),
//                    order.OrderNumber.ToString(),
//                    order.DateTime
//                );

//                await _eventHub.PublishAsync(orderEvent);
//                _logger?.LogDebug("✅ Опубликовано событие OrderCreated: {Ticker} {Side}",
//                    order.Ticker, order.Side);
//            }
//            catch (Exception ex)
//            {
//                _logger?.LogError(ex, "❌ Ошибка при публикации события OrderCreated");
//            }
//        }



//        public override void ProcessTick(ITick tick)
//        {
//            if (!IsRunning()) return;

//            try
//            {
//                base.ProcessTick(tick);

//                // Публикуем сигнал стратегии
//                var currentAction = GetCurrentAction();
//                var signalEvent = new StrategySignalEvent(
//                    tick.Ticker,
//                    StrategyName,
//                    currentAction.ToString(),
//                    tick.Price,
//                    1,
//                    tick.DateTime
//                );

//                _ = _eventHub.PublishAsync(signalEvent);

//                //_logger?.LogTrace("Стратегия {Name} обработала тик: {Ticker} {Price}",
//                //    StrategyName, tick.Ticker, tick.Price);
//            }
//            catch (Exception ex)
//            {
//                _logger?.LogError(ex, "Ошибка в ProcessTick стратегии {Name}", StrategyName);
//            }
//        }
//    }
//}
