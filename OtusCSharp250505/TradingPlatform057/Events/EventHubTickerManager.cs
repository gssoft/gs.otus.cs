// Events/EventHubTickerManager.cs

using BusLibrary02.Core;
using GS.Trade.Strategies;
using TradingPlatform.Events;
using Microsoft.Extensions.Logging;

namespace TradingPlatform.Services;

public class EventHubTickerManager
{
    private readonly List<EventHubTicker> _tickers = new();
    private readonly IEventHub _eventHub;
    private readonly ILogger<EventHubTickerManager> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public EventHubTickerManager(
        IEventHub eventHub,
        ILoggerFactory loggerFactory,
        ILogger<EventHubTickerManager> logger)
    {
        _eventHub = eventHub;
        _loggerFactory = loggerFactory;
        _logger = logger;

        InitializeTickers();
        NotifyStrategiesInitialized();
    }

    private void InitializeTickers()
    {
        try
        {
            _logger.LogInformation("🔄 Начинаю инициализацию тикеров...");

            var tickerSymbols = new[]
            {
                "AAA", "BBB", "CCC", "DDD", "EEE", "FFF", "GGG", "HHH",
                "III", "JJJ", "KKK", "LLL", "MMM", "NNN", "OOO", "PPP",
                "QQQ", "RRR", "SSS", "TTT", "UUU", "VVV", "WWW", "XXX"
            };

            // var tickerSymbols = new[]
            //{
            //     "AAPL", "AMZN", "GOOG", "BABA", "TSLA", "NFLX", "NVDA", "PYPL",
            //     "INTC", "CSCO", "ORCL", "ADBE", "MRNA", "ABBV", "GILD", "SBUX",
            //     "EBAY", "BRKB", "URBN", "FOXA", "GRMN", "HBAN", "SWKS", "JKHY"
            // };

            //var tickerSymbols = new[]
            //{
            //    "AAA", "BBB", "CCC"
            //};

            _logger.LogDebug("Будет создано {Count} тикеров", tickerSymbols.Length);

            for (int i = 0; i < tickerSymbols.Length; i++)
            {
                int uniqueSeed = CalculateUniqueSeed(tickerSymbols[i], i);
                decimal initialPrice = 1000m + (i * 25);

                _logger.LogDebug("Создаю тикер {Symbol} (seed: {Seed}, initial price: {Price})",
                    tickerSymbols[i], uniqueSeed, initialPrice);

                var ticker = new EventHubTicker(
                    tickerSymbols[i],
                    uniqueSeed,
                    initialPrice,
                    _eventHub
                )
                {
                    Id = i + 1
                };

                // Добавляем стратегии с разными периодами и логгерами
                ticker.Strategies.Add(new EventHubStrategy(
                    ticker.Symbol,
                    5,
                    $"{ticker.Symbol}_05s",
                    _eventHub,
                    _loggerFactory.CreateLogger<EventHubStrategy>()));

                ticker.Strategies.Add(new EventHubStrategy(
                    ticker.Symbol,
                    10,
                    $"{ticker.Symbol}_10s",
                    _eventHub,
                    _loggerFactory.CreateLogger<EventHubStrategy>()));

                ticker.Strategies.Add(new EventHubStrategy(
                    ticker.Symbol,
                    15,
                    $"{ticker.Symbol}_15s",
                    _eventHub,
                    _loggerFactory.CreateLogger<EventHubStrategy>()));

                _tickers.Add(ticker);

                _logger.LogDebug("✅ Тикер {Symbol} создан с {StrategiesCount} стратегиями",
                    ticker.Symbol, ticker.Strategies.Count);
            }

            _logger.LogInformation("✅ Инициализация завершена: {TickersCount} тикеров, {TotalStrategies} стратегий",
                _tickers.Count, GetTotalStrategiesCount());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при инициализации тикеров");
            throw;
        }
    }

    private void NotifyStrategiesInitialized()
    {
        try
        {
            var totalStrategies = GetTotalStrategiesCount();

            _logger.LogInformation("📊 Итоговая статистика: {TickersCount} тикеров × 3 стратегии = {TotalStrategies} стратегий",
                _tickers.Count, totalStrategies);

            // Публикуем событие о завершении инициализации
            _ = _eventHub.PublishAsync(new SystemStatusEvent(
                "TickerManager",
                "Initialized",
                $"Инициализировано {_tickers.Count} тикеров с {totalStrategies} стратегиями",
                DateTime.Now
            ));

            _logger.LogDebug("✅ Событие инициализации тикеров опубликовано");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при публикации события инициализации");
        }
    }

    private int CalculateUniqueSeed(string symbol, int index)
    {
        int asciiSeed = 0;
        foreach (char c in symbol)
        {
            asciiSeed += c;
        }

        // Создаем уникальный seed на основе символа и индекса
        int seed = Math.Abs((asciiSeed * 1000) + index) + 100;

        _logger.LogTrace("Calculated seed for {Symbol}: {Seed}", symbol, seed);
        return seed;
    }

    public IReadOnlyList<EventHubTicker> GetAllTickers() => _tickers;

    public EventHubTicker? GetTicker(string symbol)
    {
        var ticker = _tickers.FirstOrDefault(t => t.Symbol == symbol);

        if (ticker == null)
        {
            _logger.LogWarning("Тикер {Symbol} не найден", symbol);
        }

        return ticker;
    }

    public int GetTotalStrategiesCount()
    {
        return _tickers.Sum(t => t.Strategies.Count);
    }

    public Dictionary<string, int> GetStrategiesSummary()
    {
        var summary = new Dictionary<string, int>();

        foreach (var ticker in _tickers)
        {
            summary[ticker.Symbol] = ticker.Strategies.Count;
        }

        return summary;
    }

    public void LogTickersInfo()
    {
        _logger.LogInformation("📊 Информация о тикерах:");

        foreach (var ticker in _tickers)
        {
            _logger.LogInformation("  • {Symbol} (ID: {Id}): {StrategiesCount} стратегий, Последняя цена: {LastPrice:F2}",
                ticker.Symbol, ticker.Id, ticker.Strategies.Count, ticker.LastPrice);
        }
    }

    public async Task<bool> StartAllStrategiesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("🚀 Запускаю все стратегии...");
            int startedCount = 0;

            foreach (var ticker in _tickers)
            {
                foreach (var strategy in ticker.Strategies)
                {
                    if (strategy is EventHubStrategy eventHubStrategy)
                    {
                        eventHubStrategy.StartTrading(ct);
                        startedCount++;

                        // Задержка для равномерного запуска
                        if (startedCount % 10 == 0)
                        {
                            await Task.Delay(100, ct);
                        }
                    }
                }
            }

            _logger.LogInformation("✅ Запущено {StartedCount} стратегий", startedCount);

            // Публикуем событие
            await _eventHub.PublishAsync(new SystemStatusEvent(
                "TickerManager",
                "StrategiesStarted",
                $"Запущено {startedCount} стратегий",
                DateTime.Now
            ), ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при запуске стратегий");
            return false;
        }
    }

    public void StopAllStrategies()
    {
        try
        {
            _logger.LogInformation("🛑 Останавливаю все стратегии...");
            int stoppedCount = 0;

            foreach (var ticker in _tickers)
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

            _logger.LogInformation("✅ Остановлено {StoppedCount} стратегий", stoppedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при остановке стратегий");
        }
    }
}
