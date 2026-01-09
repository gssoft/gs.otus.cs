// Services/TradingLogService.cs
using BusLibrary02.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingPlatform.Events;
using TradingPlatform.Visualization;

namespace TradingPlatform.Services
{
    public class TradingLogService : BackgroundService
    {
        private readonly ILogger<TradingLogService> _logger;
        private readonly IInMemoryLogDatabase _logDatabase;
        private readonly IDynamicSubscriptionManager _subscriptionManager;
        private readonly List<IDisposable> _subscriptions = new();

        public TradingLogService(
            ILogger<TradingLogService> logger,
            IInMemoryLogDatabase logDatabase,
            IDynamicSubscriptionManager subscriptionManager)
        {
            _logger = logger;
            _logDatabase = logDatabase;
            _subscriptionManager = subscriptionManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📝 TradingLogService запущен");

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            SubscribeToTradingEvents();

            _logger.LogInformation("✅ TradingLogService подписался на события");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в TradingLogService");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("TradingLogService остановлен");
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
                        try
                        {
                            _logDatabase.AddTradeLog(
                                tradeEvent.Symbol,
                                tradeEvent.StrategyName,
                                $"TRADE: {tradeEvent.Symbol} {tradeEvent.Side} {tradeEvent.Quantity} @ {tradeEvent.Price:F2} by {tradeEvent.StrategyName}",
                                $"📊 <strong>TRADE</strong>: {tradeEvent.Symbol} {tradeEvent.Side} {tradeEvent.Quantity} @ {tradeEvent.Price:F2} by {tradeEvent.StrategyName}"
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка логирования сделки");
                        }
                    }));

                // Закрытые сделки
                _subscriptions.Add(_subscriptionManager.Subscribe<DealClosedEvent>(
                    "deal:closed",
                    async (dealEvent, ct) =>
                    {
                        try
                        {
                            _logDatabase.AddDealLog(
                                dealEvent.Symbol,
                                dealEvent.StrategyName,
                                $"DEAL CLOSED: {dealEvent.Symbol} PnL={dealEvent.PnL:F2}",
                                $"💰 <strong>DEAL CLOSED</strong>: {dealEvent.Symbol} PnL={dealEvent.PnL:F2}"
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка логирования закрытой сделки");
                        }
                    }));

                // Позиции
                _subscriptions.Add(_subscriptionManager.Subscribe<PositionChangedEvent>(
                    "position:changed",
                    async (positionEvent, ct) =>
                    {
                        try
                        {
                            if (Math.Abs(positionEvent.NetQuantity) > 0)
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
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка логирования позиции");
                        }
                    }));

                // Системные события (только ошибки)
                _subscriptions.Add(_subscriptionManager.Subscribe<SystemStatusEvent>(
                    "system:status",
                    async (systemEvent, ct) =>
                    {
                        try
                        {
                            if (systemEvent.Status == "Error")
                            {
                                _logDatabase.AddSystemLog(
                                    "System",
                                    "Error",
                                    $"{systemEvent.Component}: {systemEvent.Message}",
                                    $"❌ <strong>ERROR</strong>: {systemEvent.Component}: {systemEvent.Message}"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка логирования системного события");
                        }
                    }));

                _logger.LogInformation("✅ Подписка на торговые события завершена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при подписке на события");
            }
        }

        public override void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
            base.Dispose();
        }
    }
}