// Events/EventHubStrategy.cs
// Events/EventHubStrategy.cs (упрощенная версия)
using BusLibrary02.Core;
using GS.Trade.Abstractions;
using GS.Trade.Strategies;
using Microsoft.Extensions.Logging;
using System.Reflection;
using TradingPlatform.Events;
using TradingPlatform.Visualization;

namespace TradingPlatform.Services
{
    public class EventHubStrategy : EventDrivenRandomStrategy01
    {
        private readonly IEventHub _eventHub;
        private readonly ILogger<EventHubStrategy>? _logger;
        private readonly IInMemoryLogDatabase? _logDatabase;

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
            IEventHub eventHub, ILogger<EventHubStrategy>? logger = null,
            IInMemoryLogDatabase? logDatabase = null)
            : base(ticker, cycleTicks, strategyName)
        {
            _eventHub = eventHub;
            _logger = logger;
            _logDatabase = logDatabase;

            // Подписываемся на события стратегии
            this.OnTradeExecuted += OnTradeExecutedHandler;
            this.OnDealClosed += OnDealClosedHandler;
            this.OnPositionChanged += OnPositionChangedHandler;
            this.OnOrderCreated += OnOrderCreatedHandler;
        }

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

                // Логируем в консоль
                var consoleMessage = $"📊 TRADE: {trade.Ticker} {trade.Side} {trade.Qty} @ {trade.Price} ({StrategyName})";
                _logger?.LogInformation(consoleMessage);

                // Записываем в базу логов (только торговые события)
                if (_logDatabase != null)
                {
                    var log = new TradingLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Ticker = trade.Ticker,
                        Strategy = StrategyName,
                        Level = TradingLog.DetermineLevel(consoleMessage),
                        Category = TradingLog.DetermineCategory(consoleMessage),
                        Message = CleanMessage(consoleMessage),
                        FormattedMessage = consoleMessage,
                        Price = trade.Price,
                        Quantity = trade.Qty,
                        Side = trade.Side.ToString()
                    };

                    _logDatabase.AddLog(log);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ Ошибка при публикации события TradeExecuted: {ex.Message}";
                _logger?.LogError(ex, errorMessage);

                if (_logDatabase != null)
                {
                    var log = new TradingLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Ticker = trade.Ticker,
                        Strategy = StrategyName,
                        Level = "Error",
                        Category = "Error",
                        Message = errorMessage,
                        FormattedMessage = errorMessage
                    };

                    _logDatabase.AddLog(log);
                }
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

                var consoleMessage = $"💰 DEAL CLOSED: {deal.Ticker} {strategyName}: PnL={deal.PnL:F2}";
                _logger?.LogInformation(consoleMessage);

                // Записываем в базу логов
                if (_logDatabase != null)
                {
                    var log = new TradingLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Ticker = deal.Ticker,
                        Strategy = strategyName,
                        Level = "Information",
                        Category = "Deal",
                        Message = CleanMessage(consoleMessage),
                        FormattedMessage = consoleMessage,
                        Price = deal.ClosePrice,
                        Quantity = deal.Qty,
                        Side = deal.Side.ToString()
                    };

                    _logDatabase.AddLog(log);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ Ошибка при публикации события DealClosed: {ex.Message}";
                _logger?.LogError(ex, errorMessage);
            }
        }

        private async void OnPositionChangedHandler(IPositionSummary position)
        {
            if (!IsRunning()) return;

            try
            {
                var positionEvent = new PositionChangedEvent(
                    position.Ticker,
                    StrategyName,
                    position.NetQuantity,
                    position.UnrealizedProfit,
                    position.Status.ToString(),
                    DateTime.Now
                );

                await _eventHub.PublishAsync(positionEvent);

                // Логируем только ненулевые позиции
                if (Math.Abs(position.NetQuantity) > 0)
                {
                    var consoleMessage = $"📈 POSITION: {position.Ticker} {StrategyName}: NetQty={position.NetQuantity} | P&L={position.UnrealizedProfit:F2}";
                    _logger?.LogInformation(consoleMessage);

                    // Записываем в базу логов
                    if (_logDatabase != null)
                    {
                        var log = new TradingLog
                        {
                            Timestamp = DateTime.UtcNow,
                            Ticker = position.Ticker,
                            Strategy = StrategyName,
                            Level = "Information",
                            Category = "Position",
                            Message = CleanMessage(consoleMessage),
                            FormattedMessage = consoleMessage
                        };

                        _logDatabase.AddLog(log);
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ Ошибка при публикации события PositionChanged: {ex.Message}";
                _logger?.LogError(ex, errorMessage);
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

                var consoleMessage = $"📝 ORDER: {order.Ticker} {order.Side} {order.Qty} @ {order.Price} ({StrategyName})";
                _logger?.LogDebug(consoleMessage);

                // Записываем в базу логов
                if (_logDatabase != null)
                {
                    var log = new TradingLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Ticker = order.Ticker,
                        Strategy = StrategyName,
                        Level = "Information",
                        Category = "Order",
                        Message = CleanMessage(consoleMessage),
                        FormattedMessage = consoleMessage,
                        Price = order.Price,
                        Quantity = order.Qty,
                        Side = order.Side.ToString()
                    };

                    _logDatabase.AddLog(log);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ Ошибка при публикации события OrderCreated: {ex.Message}";
                _logger?.LogError(ex, errorMessage);
            }
        }

        public override void ProcessTick(ITick tick)
        {
            if (!IsRunning()) return;

            try
            {
                base.ProcessTick(tick);

                // Обрабатываем только изменение действия
                var currentAction = GetCurrentAction();

                // Логируем изменения в состоянии стратегии (но не каждую котировку!)
                // Например, когда стратегия меняет состояние
                // Эта логика должна быть в базовом классе EventDrivenRandomStrategy01
                // Мы просто обрабатываем здесь тик
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ Ошибка в ProcessTick стратегии {StrategyName}: {ex.Message}";
                _logger?.LogError(ex, errorMessage);
            }
        }

        private string CleanMessage(string message)
        {
            // Убираем эмодзи для чистого текста
            return message
                .Replace("📊", "TRADE")
                .Replace("💰", "DEAL")
                .Replace("📈", "POSITION")
                .Replace("📝", "ORDER")
                .Replace("❌", "ERROR")
                .Replace("⚠️", "WARNING")
                .Replace("✅", "OK")
                .Trim();
        }

        public new void StartTrading(CancellationToken cancellationToken = default)
        {
            base.StartTrading(cancellationToken);

            var message = $"🚀 Стратегия {StrategyName} для {Ticker} запущена";
            _logger?.LogInformation(message);

            // Записываем в базу логов
            if (_logDatabase != null)
            {
                var log = new TradingLog
                {
                    Timestamp = DateTime.UtcNow,
                    Ticker = Ticker,
                    Strategy = StrategyName,
                    Level = "Information",
                    Category = "System",
                    Message = CleanMessage(message),
                    FormattedMessage = message
                };

                _logDatabase.AddLog(log);
            }
        }

        public new void StopTrading()
        {
            base.StopTrading();

            var message = $"🛑 Стратегия {StrategyName} для {Ticker} остановлена";
            _logger?.LogInformation(message);

            // Записываем в базу логов
            if (_logDatabase != null)
            {
                var log = new TradingLog
                {
                    Timestamp = DateTime.UtcNow,
                    Ticker = Ticker,
                    Strategy = StrategyName,
                    Level = "Information",
                    Category = "System",
                    Message = CleanMessage(message),
                    FormattedMessage = message
                };

                _logDatabase.AddLog(log);
            }
        }

        public bool IsRunning() => GetIsRunning();
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
//        private readonly IInMemoryLogDatabase? _logDatabase;

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
//            IEventHub eventHub, ILogger<EventHubStrategy>? logger = null,
//            IInMemoryLogDatabase? logDatabase = null)
//            : base(ticker, cycleTicks, strategyName)
//        {
//            _eventHub = eventHub;
//            _logger = logger;
//            _logDatabase = logDatabase;

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
//                //_logger?.LogInformation("📊 TRADE: {Ticker} {Side} {Qty} @ {Price} ({Strategy})",
//                //    trade.Ticker, trade.Side, trade.Qty, trade.Price, StrategyName);

//                // Важные действия логируем как Information
//                var logMessage = $"📊 TRADE: {trade.Ticker} {trade.Side} {trade.Qty} @ {trade.Price} ({StrategyName})";
//                _logger?.LogInformation(logMessage);

//                // Также записываем в нашу базу логов
//                _logDatabase?.AddLog(logMessage, trade.Ticker, StrategyName);
//            }
//            catch (Exception ex)
//            {
//                // _logger?.LogError(ex, "❌ Ошибка при публикации события TradeExecuted");

//                var errorMessage = $"❌ Ошибка при публикации события TradeExecuted: {ex.Message}";
//                _logger?.LogError(ex, errorMessage);
//                _logDatabase?.AddLog(errorMessage, trade.Ticker, StrategyName);
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
