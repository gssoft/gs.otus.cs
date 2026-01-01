// Core/Ticker.cs

using GS.Trade.Strategies;
using GS.Trade.Abstractions;

namespace TradingPlatform.Core
{
    public class Ticker
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public List<EventDrivenRandomStrategy01> Strategies { get; set; } = new();
        public decimal LastPrice { get; set; }

        private readonly QuoteEngine _quoteEngine;

        public Ticker(string symbol, int uniqueSeed, decimal initialPrice = 1000m)
        {
            Symbol = symbol;
            _quoteEngine = new QuoteEngine(uniqueSeed, 80, initialPrice);
            _quoteEngine.InitializeQuotes();
        }

        public Quote GetCurrentQuote()
        {
            var quote = _quoteEngine.GetCurrentQuote();
            LastPrice = quote.Close;
            return quote;
        }

        public List<Quote> GetChartData()
        {
            return _quoteEngine.GetQuoteHistory();
        }

        public void GenerateNextQuote()
        {
            _quoteEngine.GenerateNextQuote();
        }

        public (double[] timeStamps, double[] highData, double[] lowData,
                double[] openData, double[] closeData, double[] volData) GetRawData()
        {
            return _quoteEngine.GetChartDirectorData();
        }

        public string GetStats()
        {
            return _quoteEngine.GetStats();
        }
    }

    public class Quote
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
    }
}
