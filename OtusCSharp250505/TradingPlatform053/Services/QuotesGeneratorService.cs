// Services/QuotesGeneratorService.cs

using BusLibrary02.Core;
using TradingPlatform.Services;

using TradingPlatform.Events;

public class QuotesGeneratorService : BackgroundService
{
    private readonly ILogger<QuotesGeneratorService> _logger;
    private readonly EventHubTickerManager _tickerManager;
    private readonly IEventHub _eventHub;
    private int _iteration = 0;

    public QuotesGeneratorService(
        ILogger<QuotesGeneratorService> logger,
        EventHubTickerManager tickerManager,
        IEventHub eventHub)
    {
        _logger = logger;
        _tickerManager = tickerManager;
        _eventHub = eventHub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 QuotesGeneratorService запущен");

        // Публикуем системное событие
        await _eventHub.PublishAsync(new SystemStatusEvent(
            "QuotesGenerator",
            "Started",
            "Сервис генерации котировок запущен",
            DateTime.Now
        ), stoppingToken);

        // Ждем немного перед началом генерации
        await Task.Delay(2000, stoppingToken);

        _iteration = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _iteration++;

                foreach (var ticker in _tickerManager.GetAllTickers())
                {
                    // Генерируем новую котировку
                    ticker.GenerateNextQuote();

                    // Получаем текущую котировку
                    var quote = ticker.GetCurrentQuote();

                    // Публикуем событие котировки через EventHub
                    var quoteEvent = new QuoteGeneratedEvent(
                        ticker.Symbol,
                        quote.Open,
                        quote.High,
                        quote.Low,
                        quote.Close,
                        quote.Volume,
                        quote.Timestamp
                    );

                    await _eventHub.PublishAsync(quoteEvent, stoppingToken);

                    // Логируем каждую 5-ю котировку
                    if (_iteration % 5 == 0)
                    {
                        _logger.LogInformation(
                            "📊 Generated quote #{Iteration} for {Symbol}: {Close:F2}",
                            _iteration, ticker.Symbol, quote.Close);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Generated quote for {Symbol}: {Close:F2}",
                            ticker.Symbol, quote.Close);
                    }
                }

                // Логируем каждые 10 итераций
                if (_iteration % 10 == 0)
                {
                    _logger.LogInformation(
                        "🔄 QuotesGeneratorService: Обработано {Iteration} итераций",
                        _iteration);
                }

                await Task.Delay(2000, stoppingToken); // Генерируем каждые 2 секунды
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при генерации котировок");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("🛑 QuotesGeneratorService остановлен");
    }
}

