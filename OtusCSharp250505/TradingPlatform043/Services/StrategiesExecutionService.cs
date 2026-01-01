// Services/StrategiesExecutionService.cs

using BusLibrary02.Core;
using GS.Trade.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingPlatform.Events;
using TradingPlatform.Services;
using System.Collections.Concurrent;

namespace TradingPlatform.Services
{
    /// <summary>
    /// Сервис выполнения стратегий - изолированно обрабатывает котировки для всех стратегий
    /// </summary>
    public class StrategiesExecutionService : BackgroundService
    {
        private readonly ILogger<StrategiesExecutionService> _logger;
        private readonly EventHubTickerManager _tickerManager;
        private readonly IDynamicSubscriptionManager _subscriptionManager;
        private readonly ITradingFactory _tradingFactory;
        private IDisposable? _quoteSubscription;

        // Статистика производительности
        private readonly ConcurrentDictionary<string, int> _processedCounts = new();
        private DateTime _startTime;
        private long _totalTicksProcessed = 0;

        public StrategiesExecutionService(
            ILogger<StrategiesExecutionService> logger,
            EventHubTickerManager tickerManager,
            IDynamicSubscriptionManager subscriptionManager,
            ITradingFactory tradingFactory)
        {
            _logger = logger;
            _tickerManager = tickerManager;
            _subscriptionManager = subscriptionManager;
            _tradingFactory = tradingFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _startTime = DateTime.UtcNow;
            _logger.LogInformation("🚀 StrategiesExecutionService запущен");

            // 1. Регистрируем ключ для подписки
            _subscriptionManager.RegisterStaticKey<QuoteGeneratedEvent>("quote:generated");

            // 2. Запускаем все стратегии
            await StartAllStrategies(stoppingToken);

            // 3. Подписываемся на котировки
            await SubscribeToQuotes(stoppingToken);

            // 4. Мониторинг производительности
            _ = Task.Run(() => MonitorPerformance(stoppingToken), stoppingToken);

            _logger.LogInformation("✅ Все стратегии запущены, подписка на котировки активна");

            // Ждем остановки
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("StrategiesExecutionService получил запрос на остановку");
            }
            finally
            {
                await StopAllStrategies();
                _quoteSubscription?.Dispose();

                _logger.LogInformation("🛑 StrategiesExecutionService остановлен. " +
                    "Обработано тиков: {TotalTicks}", _totalTicksProcessed);
            }
        }

        /// <summary>
        /// Запуск всех стратегий
        /// </summary>
        private async Task StartAllStrategies(CancellationToken ct)
        {
            try
            {
                var tickers = _tickerManager.GetAllTickers();
                int totalStarted = 0;

                foreach (var ticker in tickers)
                {
                    foreach (var strategy in ticker.Strategies)
                    {
                        if (strategy is EventHubStrategy eventHubStrategy)
                        {
                            eventHubStrategy.StartTrading(ct);
                            totalStarted++;

                            _logger.LogDebug("▶️ Запущена стратегия {StrategyName} для {Ticker}",
                                eventHubStrategy.StrategyName, ticker.Symbol);
                        }
                    }

                    // Небольшая задержка между тикерами для равномерного запуска
                    await Task.Delay(50, ct);
                }

                _logger.LogInformation("✅ Все стратегии ({Count}) успешно запущены", totalStarted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при запуске стратегий");
                throw;
            }
        }

        /// <summary>
        /// Подписка на котировки из EventHub
        /// </summary>
        private async Task SubscribeToQuotes(CancellationToken ct)
        {
            _quoteSubscription = _subscriptionManager.Subscribe<QuoteGeneratedEvent>(
                async (quote, ct) =>
                {
                    try
                    {
                        await ProcessQuoteForStrategies(quote, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Ошибка обработки котировки для {Symbol}", quote.Symbol);
                    }
                });

            // Небольшая пауза для стабилизации подписки
            await Task.Delay(1000, ct);

            _logger.LogInformation("📡 Подписка на котировки активирована");
        }

        /// <summary>
        /// Обработка котировки и передача во все стратегии тикера
        /// </summary>
        private async Task ProcessQuoteForStrategies(QuoteGeneratedEvent quote, CancellationToken ct)
        {
            // Находим тикер по символу
            var ticker = _tickerManager.GetTicker(quote.Symbol);
            if (ticker == null)
            {
                _logger.LogWarning("Тикер {Symbol} не найден в менеджере", quote.Symbol);
                return;
            }

            // Обновляем статистику
            _processedCounts.AddOrUpdate(quote.Symbol, 1, (_, count) => count + 1);
            Interlocked.Increment(ref _totalTicksProcessed);

            // Создаем тик из котировки
            var tick = _tradingFactory.CreateTick(
                ticker: quote.Symbol,
                price: quote.Close,
                volume: quote.Volume,
                dateTime: quote.Timestamp,
                tickNumber: DateTime.UtcNow.Ticks
            );

            // Передаем тик во ВСЕ стратегии этого тикера
            bool anyStrategyProcessed = false;
            foreach (var strategy in ticker.Strategies)
            {
                if (strategy is EventHubStrategy eventHubStrategy && eventHubStrategy.IsRunning())
                {
                    try
                    {
                        // Синхронный вызов - стратегия должна обрабатывать быстро
                        eventHubStrategy.ProcessTick(tick);
                        anyStrategyProcessed = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Ошибка в стратегии {StrategyName} при обработке тика {Symbol}",
                            eventHubStrategy.StrategyName, quote.Symbol);
                    }
                }
            }

            if (!anyStrategyProcessed)
            {
                _logger.LogDebug("Котировка {Symbol} не обработана ни одной стратегией (возможно, все остановлены)",
                    quote.Symbol);
            }

            // Логируем каждую 100-ю котировку
            if (_totalTicksProcessed % 100 == 0)
            {
                _logger.LogDebug("📊 Обработано {TotalTicks} тиков, последний: {Symbol} @ {Close:F2}",
                    _totalTicksProcessed, quote.Symbol, quote.Close);
            }
        }

        /// <summary>
        /// Мониторинг производительности
        /// </summary>
        /// 

        private async Task MonitorPerformance(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(30000, ct); // Каждые 30 секунд

                var elapsed = DateTime.UtcNow - _startTime;
                var ticksPerSecond = elapsed.TotalSeconds > 0
                    ? _totalTicksProcessed / elapsed.TotalSeconds
                    : 0;

                // Логируем как Information каждые 30 секунд
                _logger.LogInformation(
                    "📈 Perf: {Ticks} тиков за {Time:mm\\:ss} ({Rate:F1}/сек), " +
                    "Тикеров: {Tickers}, Стратегий: {Strategies}",
                    _totalTicksProcessed,
                    elapsed,
                    ticksPerSecond,
                    _processedCounts.Count,
                    _tickerManager.GetTotalStrategiesCount());
            }
        }

        //private async Task MonitorPerformance(CancellationToken ct)
        //{
        //    while (!ct.IsCancellationRequested)
        //    {
        //        await Task.Delay(30000, ct); // Каждые 30 секунд

        //        var elapsed = DateTime.UtcNow - _startTime;
        //        var ticksPerSecond = elapsed.TotalSeconds > 0
        //            ? _totalTicksProcessed / elapsed.TotalSeconds
        //            : 0;

        //        _logger.LogInformation(
        //            "📈 Perf: {Ticks} тиков за {Time:mm\\:ss} ({Rate:F1}/сек), " +
        //            "Тикеров: {Tickers}, Стратегий: {Strategies}",
        //            _totalTicksProcessed,
        //            elapsed,
        //            ticksPerSecond,
        //            _processedCounts.Count,
        //            _tickerManager.GetTotalStrategiesCount());

        //        // Детальная статистика по тикерам (топ 5)
        //        var topTickers = _processedCounts
        //            .OrderByDescending(kv => kv.Value)
        //            .Take(5)
        //            .Select(kv => $"{kv.Key}:{kv.Value}");

        //        if (topTickers.Any())
        //        {
        //            _logger.LogDebug("🏆 Топ тикеров: {Tickers}", string.Join(", ", topTickers));
        //        }
        //    }
        //}

        /// <summary>
        /// Остановка всех стратегий
        /// </summary>
        private async Task StopAllStrategies()
        {
            try
            {
                int stoppedCount = 0;
                var tickers = _tickerManager.GetAllTickers();

                foreach (var ticker in tickers)
                {
                    foreach (var strategy in ticker.Strategies)
                    {
                        if (strategy is EventHubStrategy eventHubStrategy)
                        {
                            eventHubStrategy.StopTrading();
                            stoppedCount++;
                        }
                    }
                }

                _logger.LogInformation("🛑 Остановлено {Count} стратегий", stoppedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при остановке стратегий");
            }
        }
    }
}

