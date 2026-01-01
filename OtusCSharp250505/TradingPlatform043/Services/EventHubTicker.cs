// Service/EventHubTicker.cs

using TradingPlatform.Core;
using TradingPlatform.Events;

using BusLibrary02.Core;

namespace TradingPlatform.Services;

public class EventHubTicker : Ticker
{
    private readonly IEventHub _eventHub;

    public EventHubTicker(string symbol, int uniqueSeed, decimal initialPrice, IEventHub eventHub)
        : base(symbol, uniqueSeed, initialPrice)
    {
        _eventHub = eventHub;
    }

    public new void GenerateNextQuote()
    {
        base.GenerateNextQuote();
        var quote = GetCurrentQuote();

        // Публикуем событие новой котировки
        var quoteEvent = new QuoteGeneratedEvent(
            Symbol,
            quote.Open,
            quote.High,
            quote.Low,
            quote.Close,
            quote.Volume,
            quote.Timestamp
        );

      //  _ = _eventHub.PublishAsync(quoteEvent);

        // Публикуем событие для обновления графика
        var chartEvent = new ChartUpdateEvent(Symbol, DateTime.Now);
        _ = _eventHub.PublishAsync(chartEvent);
    }
}