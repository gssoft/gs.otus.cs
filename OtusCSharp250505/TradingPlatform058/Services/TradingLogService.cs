// Services/TradingLogService.cs
// Services/TradingLogService.cs - добавим глобальную дедупликацию
using BusLibrary02.Core;
using System.Collections.Concurrent;
using TradingPlatform.Events;
using TradingPlatform.Services;
using TradingPlatform.Visualization;

public class TradingLogService : BackgroundService
{
    private readonly ILogger<TradingLogService> _logger;
    private readonly IInMemoryLogDatabase _logDatabase;
    private readonly IDynamicSubscriptionManager _subscriptionManager;
    private readonly List<IDisposable> _subscriptions = new();

    // Глобальный кэш для дедупликации всех событий
    private readonly ConcurrentDictionary<string, (string Hash, DateTime Time)> _globalEventCache = new();
    private readonly TimeSpan _globalEventCooldown = TimeSpan.FromMilliseconds(500);

    // Статистика
    private int _processedEvents = 0;
    private int _duplicateEvents = 0;

    public TradingLogService(
        ILogger<TradingLogService> logger,
        IInMemoryLogDatabase logDatabase,
        IDynamicSubscriptionManager subscriptionManager)
    {
        _logger = logger;
        _logDatabase = logDatabase;
        _subscriptionManager = subscriptionManager;
    }

    // Метод для создания хэша события
    private string CreateEventHash(string eventType, string ticker, string strategy, params object[] additionalData)
    {
        var data = $"{eventType}|{ticker}|{strategy}|{string.Join("|", additionalData.Select(d => d?.ToString() ?? ""))}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data));
    }

    // Метод для проверки и добавления события в кэш
    private bool IsDuplicateEvent(string eventHash)
    {
        if (_globalEventCache.TryGetValue(eventHash, out var cached))
        {
            if (DateTime.UtcNow - cached.Time < _globalEventCooldown)
            {
                return true; // Дубликат
            }
        }

        // Обновляем или добавляем в кэш
        _globalEventCache[eventHash] = (eventHash, DateTime.UtcNow);
        return false; // Не дубликат
    }

    private void SubscribeToTradingEvents()
    {
        _logger.LogInformation("🔄 Подписываюсь на торговые события для логирования...");

        try
        {
            // Сделки
            _subscriptions.Add(_subscriptionManager.Subscribe<TradeExecutedEvent>(
                "trade:executed",
                async (tradeEvent, ct) =>
                {
                    Interlocked.Increment(ref _processedEvents);

                    var eventHash = CreateEventHash("TRADE", tradeEvent.Symbol, tradeEvent.StrategyName,
                        tradeEvent.Side, tradeEvent.Price, tradeEvent.Quantity);

                    if (IsDuplicateEvent(eventHash))
                    {
                        Interlocked.Increment(ref _duplicateEvents);
                        _logger.LogDebug("Дублирующаяся сделка пропущена: {Symbol} {Side} {Price}",
                            tradeEvent.Symbol, tradeEvent.Side, tradeEvent.Price);
                        return;
                    }

                    _logDatabase.AddLog(new TradingLog
                    {
                        Ticker = tradeEvent.Symbol,
                        Strategy = tradeEvent.StrategyName,
                        Level = "Information",
                        Category = "Trade",
                        Message = $"TRADE: {tradeEvent.Symbol} {tradeEvent.Side} {tradeEvent.Quantity} @ {tradeEvent.Price:F2}",
                        FormattedMessage = $"📊 <strong>TRADE</strong>: {tradeEvent.Symbol} {tradeEvent.Side} {tradeEvent.Quantity} @ {tradeEvent.Price:F2}",
                        Timestamp = tradeEvent.Timestamp
                    });
                }));

            // Закрытые сделки
            _subscriptions.Add(_subscriptionManager.Subscribe<DealClosedEvent>(
                "deal:closed",
                async (dealEvent, ct) =>
                {
                    Interlocked.Increment(ref _processedEvents);

                    var eventHash = CreateEventHash("DEAL", dealEvent.Symbol, dealEvent.StrategyName,
                        dealEvent.DealNumber);

                    if (IsDuplicateEvent(eventHash))
                    {
                        Interlocked.Increment(ref _duplicateEvents);
                        _logger.LogDebug("Дублирующаяся закрытая сделка пропущена: #{DealNumber}",
                            dealEvent.DealNumber);
                        return;
                    }

                    _logDatabase.AddLog(new TradingLog
                    {
                        Ticker = dealEvent.Symbol,
                        Strategy = dealEvent.StrategyName,
                        Level = "Information",
                        Category = "Deal",
                        Message = $"DEAL CLOSED: {dealEvent.Symbol} PnL={dealEvent.PnL:F2}",
                        FormattedMessage = $"💰 <strong>DEAL CLOSED</strong>: {dealEvent.Symbol} PnL={dealEvent.PnL:F2}",
                        Timestamp = dealEvent.Timestamp
                    });
                }));

            // Позиции - логируем ТОЛЬКО если позиция изменилась
            _subscriptions.Add(_subscriptionManager.Subscribe<PositionChangedEvent>(
                "position:changed",
                async (positionEvent, ct) =>
                {
                    Interlocked.Increment(ref _processedEvents);

                    // Создаем хэш на основе тикера, стратегии и НЕТТО-количества (не P&L!)
                    var eventHash = CreateEventHash("POSITION", positionEvent.Symbol, positionEvent.StrategyName,
                        positionEvent.NetQuantity);

                    if (IsDuplicateEvent(eventHash))
                    {
                        Interlocked.Increment(ref _duplicateEvents);
                        _logger.LogTrace("Дублирующееся событие позиции пропущено: {Symbol} {Strategy} NetQty={NetQty}",
                            positionEvent.Symbol, positionEvent.StrategyName, positionEvent.NetQuantity);
                        return;
                    }

                    // Логируем только если позиция не нулевая
                    if (positionEvent.NetQuantity != 0)
                    {
                        _logDatabase.AddLog(new TradingLog
                        {
                            Ticker = positionEvent.Symbol,
                            Strategy = positionEvent.StrategyName,
                            Level = "Information",
                            Category = "Position",
                            Message = $"POSITION: {positionEvent.Symbol} NetQty={positionEvent.NetQuantity} | P&L={positionEvent.UnrealizedPnL:F2}",
                            FormattedMessage = $"📈 <strong>POSITION</strong>: {positionEvent.Symbol} NetQty={positionEvent.NetQuantity} | P&L={positionEvent.UnrealizedPnL:F2}",
                            Timestamp = positionEvent.Timestamp
                        });
                    }
                }));

            _logger.LogInformation("✅ TradingLogService подписался на {Count} типов событий", _subscriptions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при подписке на события");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📝 TradingLogService запущен");

        // Ждем инициализации других сервисов
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        SubscribeToTradingEvents();

        // Очищаем старые записи из кэша каждую минуту
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                // Очищаем старые записи из кэша (старше 5 минут)
                var cutoffTime = DateTime.UtcNow - TimeSpan.FromMinutes(5);
                var oldKeys = _globalEventCache
                    .Where(kv => kv.Value.Time < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in oldKeys)
                {
                    _globalEventCache.TryRemove(key, out _);
                }

                if (oldKeys.Count > 0)
                {
                    _logger.LogDebug("Очищен кэш событий: {Count} старых записей", oldKeys.Count);
                }

                // Логируем статистику каждые 5 минут
                if (DateTime.UtcNow.Minute % 5 == 0)
                {
                    _logger.LogInformation("📊 TradingLogService статистика: {Processed} событий, {Duplicates} дубликатов",
                        _processedEvents, _duplicateEvents);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в TradingLogService");
            }
        }

        _logger.LogInformation("📝 TradingLogService остановлен");
    }
}

//using BusLibrary02.Core;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using TradingPlatform.Events;
//using TradingPlatform.Visualization;

//namespace TradingPlatform.Services
//{
//    public class TradingLogService : BackgroundService
//    {
//        private readonly ILogger<TradingLogService> _logger;
//        private readonly IInMemoryLogDatabase _logDatabase;
//        private readonly IDynamicSubscriptionManager _subscriptionManager;
//        private readonly List<IDisposable> _subscriptions = new();

//        public TradingLogService(
//            ILogger<TradingLogService> logger,
//            IInMemoryLogDatabase logDatabase,
//            IDynamicSubscriptionManager subscriptionManager)
//        {
//            _logger = logger;
//            _logDatabase = logDatabase;
//            _subscriptionManager = subscriptionManager;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("📝 TradingLogService запущен");

//            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

//            SubscribeToTradingEvents();

//            _logger.LogInformation("✅ TradingLogService подписался на события");

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
//                }
//                catch (OperationCanceledException)
//                {
//                    break;
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Ошибка в TradingLogService");
//                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
//                }
//            }

//            _logger.LogInformation("TradingLogService остановлен");
//        }

//        private void SubscribeToTradingEvents()
//        {
//            _logger.LogInformation("🔄 Подписываюсь на торговые события для логирования...");

//            try
//            {
//                // Сделки
//                _subscriptions.Add(_subscriptionManager.Subscribe<TradeExecutedEvent>(
//                    "trade:executed",
//                    async (tradeEvent, ct) =>
//                    {
//                        try
//                        {
//                            _logDatabase.AddTradeLog(
//                                tradeEvent.Symbol,
//                                tradeEvent.StrategyName,
//                                $"TRADE: {tradeEvent.Symbol} {tradeEvent.Side} {tradeEvent.Quantity} @ {tradeEvent.Price:F2} by {tradeEvent.StrategyName}",
//                                $"📊 <strong>TRADE</strong>: {tradeEvent.Symbol} {tradeEvent.Side} {tradeEvent.Quantity} @ {tradeEvent.Price:F2} by {tradeEvent.StrategyName}"
//                            );
//                        }
//                        catch (Exception ex)
//                        {
//                            _logger.LogError(ex, "Ошибка логирования сделки");
//                        }
//                    }));

//                // Закрытые сделки
//                _subscriptions.Add(_subscriptionManager.Subscribe<DealClosedEvent>(
//                    "deal:closed",
//                    async (dealEvent, ct) =>
//                    {
//                        try
//                        {
//                            _logDatabase.AddDealLog(
//                                dealEvent.Symbol,
//                                dealEvent.StrategyName,
//                                $"DEAL CLOSED: {dealEvent.Symbol} PnL={dealEvent.PnL:F2}",
//                                $"💰 <strong>DEAL CLOSED</strong>: {dealEvent.Symbol} PnL={dealEvent.PnL:F2}"
//                            );
//                        }
//                        catch (Exception ex)
//                        {
//                            _logger.LogError(ex, "Ошибка логирования закрытой сделки");
//                        }
//                    }));

//                // Позиции
//                _subscriptions.Add(_subscriptionManager.Subscribe<PositionChangedEvent>(
//                    "position:changed",
//                    async (positionEvent, ct) =>
//                    {
//                        try
//                        {
//                            if (Math.Abs(positionEvent.NetQuantity) > 0)
//                            {
//                                _logDatabase.AddLog(new TradingLog
//                                {
//                                    Ticker = positionEvent.Symbol,
//                                    Strategy = positionEvent.StrategyName,
//                                    Level = "Information",
//                                    Category = "Position",
//                                    Message = $"POSITION: {positionEvent.Symbol} NetQty={positionEvent.NetQuantity} | P&L={positionEvent.UnrealizedPnL:F2}",
//                                    FormattedMessage = $"📈 <strong>POSITION</strong>: {positionEvent.Symbol} NetQty={positionEvent.NetQuantity} | P&L={positionEvent.UnrealizedPnL:F2}",
//                                    Timestamp = positionEvent.Timestamp
//                                });
//                            }
//                        }
//                        catch (Exception ex)
//                        {
//                            _logger.LogError(ex, "Ошибка логирования позиции");
//                        }
//                    }));

//                // Системные события (только ошибки)
//                _subscriptions.Add(_subscriptionManager.Subscribe<SystemStatusEvent>(
//                    "system:status",
//                    async (systemEvent, ct) =>
//                    {
//                        try
//                        {
//                            if (systemEvent.Status == "Error")
//                            {
//                                _logDatabase.AddSystemLog(
//                                    "System",
//                                    "Error",
//                                    $"{systemEvent.Component}: {systemEvent.Message}",
//                                    $"❌ <strong>ERROR</strong>: {systemEvent.Component}: {systemEvent.Message}"
//                                );
//                            }
//                        }
//                        catch (Exception ex)
//                        {
//                            _logger.LogError(ex, "Ошибка логирования системного события");
//                        }
//                    }));

//                _logger.LogInformation("✅ Подписка на торговые события завершена");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "❌ Ошибка при подписке на события");
//            }
//        }

//        public override void Dispose()
//        {
//            foreach (var subscription in _subscriptions)
//            {
//                subscription.Dispose();
//            }
//            _subscriptions.Clear();
//            base.Dispose();
//        }
//    }
//}