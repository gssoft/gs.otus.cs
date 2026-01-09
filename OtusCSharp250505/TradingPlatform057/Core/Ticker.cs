// Core/Ticker.cs
using GS.Trade.Strategies;
using GS.Trade.Abstractions;
using TradingPlatform.Events;

namespace TradingPlatform.Core
{
    public class Ticker
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public List<EventDrivenRandomStrategy01> Strategies { get; set; } = new();
        public decimal LastPrice { get; set; }

        private readonly QuoteEngine _quoteEngine;
        private readonly ChartBackend _chartBackend; // Единый backend

        public Ticker(string symbol, int uniqueSeed, decimal initialPrice = 1000m)
        {
            Symbol = symbol;

            // Создаем единый backend для всех данных графика
            _chartBackend = new ChartBackend(symbol);

            // Создаем движок котировок
            _quoteEngine = new QuoteEngine(uniqueSeed, 80, initialPrice);
            _quoteEngine.InitializeQuotes();

            // Инициализируем backend начальными данными
            var (timeStamps, highData, lowData, openData, closeData, volData) =
                _quoteEngine.GetChartDirectorData();
            _chartBackend.InitializeWithData(timeStamps, highData, lowData,
                                            openData, closeData, volData);
        }

        public Quote GetCurrentQuote()
        {
            var quote = _quoteEngine.GetCurrentQuote();
            LastPrice = quote.Close;
            return quote;
        }

        public void GenerateNextQuote()
        {
            _quoteEngine.GenerateNextQuote();

            // Получаем последнюю котировку и добавляем в backend
            var quote = GetCurrentQuote();
            _chartBackend.PushQuote(
                quote.Timestamp.ToOADate(),
                (double)quote.High,
                (double)quote.Low,
                (double)quote.Open,
                (double)quote.Close,
                (double)quote.Volume
            );
        }

        /// <summary>
        /// Добавление сигнала покупки
        /// </summary>
        public void PushBuySignal(decimal price)
        {
            _chartBackend.PushBuySignal((double)price);
        }

        /// <summary>
        /// Добавление сигнала продажи
        /// </summary>
        public void PushSellSignal(decimal price)
        {
            _chartBackend.PushSellSignal((double)price);
        }

        /// <summary>
        /// Обработка сделки из EventHub
        /// </summary>
        public void ProcessTradeEvent(TradeExecutedEvent tradeEvent)
        {
            if (tradeEvent.Symbol != Symbol) return;

            if (tradeEvent.Side == "Buy")
            {
                PushBuySignal(tradeEvent.Price);
            }
            else if (tradeEvent.Side == "Sell")
            {
                PushSellSignal(tradeEvent.Price);
            }
        }

        /// <summary>
        /// Получение снимка данных для рендеринга
        /// </summary>
        public ChartDataSnapshot GetChartData()
        {
            return _chartBackend.GetSnapshot();
        }

        // Для совместимости со старым кодом
        public (double[] timeStamps, double[] highData, double[] lowData,
                double[] openData, double[] closeData, double[] volData) GetRawData()
        {
            var snapshot = _chartBackend.GetSnapshot();
            return (snapshot.TimeStamps, snapshot.HighData, snapshot.LowData,
                    snapshot.OpenData, snapshot.CloseData, snapshot.VolData);
        }

        public string GetStats()
        {
            return _quoteEngine.GetStats();
        }

        //public class Quote
        //{
        //    public DateTime Timestamp { get; set; }
        //    public decimal Open { get; set; }
        //    public decimal High { get; set; }
        //    public decimal Low { get; set; }
        //    public decimal Close { get; set; }
        //    public long Volume { get; set; }
        //}
    }
}
