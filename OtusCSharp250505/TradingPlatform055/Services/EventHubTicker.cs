// Services/EventHubTicker.cs
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
        base.GenerateNextQuote(); // Котировка уже добавлена в backend

        // Публикуем событие новой котировки
        var quote = GetCurrentQuote();
        var quoteEvent = new QuoteGeneratedEvent(
            Symbol,
            quote.Open,
            quote.High,
            quote.Low,
            quote.Close,
            quote.Volume,
            quote.Timestamp
        );

        _ = _eventHub.PublishAsync(quoteEvent);
    }

    // ProcessTradeEvent уже реализован в базовом классе
}
