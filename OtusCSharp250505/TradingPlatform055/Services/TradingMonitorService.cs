// Services/TradingMonitorService.cs

// Services/TradingMonitorService.cs
using Serilog;

using BusLibrary02.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TradingPlatform.Events;

namespace TradingPlatform.Services
{
    public class TradingMonitorService : BackgroundService
    {
        private readonly ILogger<TradingMonitorService> _logger;
        private readonly IEventHub _eventHub;
        private readonly IDynamicSubscriptionManager _subscriptionManager;
        private int _eventsReceived = 0;

        public TradingMonitorService(
            ILogger<TradingMonitorService> logger,
            IEventHub eventHub,
            IDynamicSubscriptionManager subscriptionManager)
        {
            _logger = logger;
            _eventHub = eventHub;
            _subscriptionManager = subscriptionManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📊 TradingMonitorService запущен");

            // Подписываемся только на торговые события (НЕ на котировки!)
            var subscriptions = new List<IDisposable>();

            // Мониторинг сделок
            subscriptions.Add(_subscriptionManager.Subscribe<TradeExecutedEvent>(
                async (trade, ct) =>
                {
                    Interlocked.Increment(ref _eventsReceived);
                    _logger.LogInformation(
                        "📊 TRADE: {Symbol} {Side} {Qty} @ {Price:F2} by {Strategy}",
                        trade.Symbol, trade.Side, trade.Quantity, trade.Price, trade.StrategyName);
                }));

            // Мониторинг позиций
            subscriptions.Add(_subscriptionManager.Subscribe<PositionChangedEvent>(
                async (position, ct) =>
                {
                    Interlocked.Increment(ref _eventsReceived);
                    _logger.LogInformation(
                        "📈 POSITION: {Symbol}: NetQty: {NetQuantity} | Unrealized P&L: {UnrealizedPnL:F2}",
                        position.Symbol, position.NetQuantity, position.UnrealizedPnL);
                }));

            try
            {
                _logger.LogInformation("✅ TradingMonitorService подписался на торговые события");

                // Ждем отмены
                try { await Task.Delay(Timeout.Infinite, stoppingToken); }
                catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken) { }
            }
            finally
            {
                // Отписываемся
                foreach (var subscription in subscriptions)
                {
                    subscription.Dispose();
                }

                _logger.LogInformation("🛑 TradingMonitorService остановлен. Всего событий: {Events}",
                    _eventsReceived);
            }
        }
    }
}

//using Serilog;

//using BusLibrary02.Core;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;

//using TradingPlatform.Events;

//namespace TradingPlatform.Services
//{
//    public class TradingMonitorService : BackgroundService
//    {
//        private readonly ILogger<TradingMonitorService> _logger;
//        private readonly IEventHub _eventHub;
//        private readonly IDynamicSubscriptionManager _subscriptionManager;
//        private int _eventsReceived = 0;

//        private readonly IInMemoryLogDatabase? _logDatabase;

//        public TradingMonitorService(
//        ILogger<TradingMonitorService> logger,
//        IEventHub eventHub,
//        IDynamicSubscriptionManager subscriptionManager,
//        IInMemoryLogDatabase? logDatabase = null) // Добавляем параметр
//        {
//            _logger = logger;
//            _eventHub = eventHub;
//            _subscriptionManager = subscriptionManager;
//            _logDatabase = logDatabase;
//        }

//        //public TradingMonitorService(
//        //    ILogger<TradingMonitorService> logger,
//        //    IEventHub eventHub,
//        //    IDynamicSubscriptionManager subscriptionManager)
//        //{
//        //    _logger = logger;
//        //    _eventHub = eventHub;
//        //    _subscriptionManager = subscriptionManager;
//        //}

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("📊 TradingMonitorService запущен");
//            _logDatabase?.AddLog("🚀 TradingMonitorService запущен");


//            // Подписываемся на интересующие нас события
//            var subscriptions = new List<IDisposable>();

//            // Мониторинг сделок
//            subscriptions.Add(_subscriptionManager.Subscribe<TradeExecutedEvent>(
//                async (trade, ct) =>
//                {
//                    Interlocked.Increment(ref _eventsReceived);
//                    //_logger.LogInformation(
//                    //    "📊 TRADE: {Symbol} {Side} {Qty} @ {Price:F2} by {Strategy}",
//                    //    trade.Symbol, trade.Side, trade.Quantity, trade.Price, trade.StrategyName);

//                    var logMessage = $"📊 TRADE: {trade.Symbol} {trade.Side} {trade.Quantity} @ {trade.Price:F2} by {trade.StrategyName}";
//                    _logger?.LogInformation(logMessage);
//                    _logDatabase?.AddLog(logMessage, trade.Symbol, trade.StrategyName);

//                }));

//            // Мониторинг котировок
//            subscriptions.Add(_subscriptionManager.Subscribe<QuoteGeneratedEvent>(
//                async (quote, ct) =>
//                {
//                    Interlocked.Increment(ref _eventsReceived);
//                    if (_eventsReceived % 10 == 0) // Логируем каждую 10-ю котировку
//                    {
//                        _logger.LogInformation(
//                            "📈 QUOTE: {Symbol} {Close:F2} (Total events: {Events})",
//                            quote.Symbol, quote.Close, _eventsReceived);
//                    }
//                }));

//            // Мониторинг позиций
//            subscriptions.Add(_subscriptionManager.Subscribe<PositionChangedEvent>(
//                async (position, ct) =>
//                {
//                    Interlocked.Increment(ref _eventsReceived);
//                    _logger.LogInformation(
//                        "📈 POSITION: {Symbol}: NetQty: {NetQuantity} | Unrealized P&L: {UnrealizedPnL:F2}",
//                        position.Symbol, position.NetQuantity, position.UnrealizedPnL);
//                }));

//            try
//            {
//                _logger.LogInformation("✅ TradingMonitorService подписался на {Count} типов событий",
//                    subscriptions.Count);

//                // Ждем отмены
//                try { await Task.Delay(Timeout.Infinite, stoppingToken); }
//                catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken) { }
//            }
//            finally
//            {
//                // Отписываемся
//                foreach (var subscription in subscriptions)
//                {
//                    subscription.Dispose();
//                }

//                _logger.LogInformation("🛑 TradingMonitorService остановлен. Всего событий: {Events}",
//                    _eventsReceived);
//            }
//        }
//    }
//}

