// Charts/ChartContainer.cs
using BusLibrary02.Core;
using ChartDirector;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core;
using TradingPlatform.Events;
using TradingPlatform.Services;

namespace TradingPlatform.Charts
{
    public class ChartContainer
    {
        private readonly List<TickerChartAdapter> _chartAdapters = new();
        private readonly EventHubTickerManager _tickerManager;
        private readonly IDynamicSubscriptionManager _subscriptionManager;
        private readonly ILogger<ChartContainer> _logger;
        private readonly List<IDisposable> _subscriptions = new();

        public ChartContainer(
            EventHubTickerManager tickerManager,
            IEventHub eventHub,
            IDynamicSubscriptionManager subscriptionManager,
            ILogger<ChartContainer> logger,
            ILoggerFactory loggerFactory)
        {
            _tickerManager = tickerManager;
            _subscriptionManager = subscriptionManager;
            _logger = logger;

            // Создаем адаптеры для всех тикеров
            foreach (var ticker in _tickerManager.GetAllTickers())
            {
                _chartAdapters.Add(new TickerChartAdapter(ticker,
                    loggerFactory.CreateLogger<TickerChartAdapter>()));
            }

            // Подписываемся на события сделок
            SubscribeToEvents();

            _logger.LogInformation("ChartContainer initialized with {Count} chart adapters",
                _chartAdapters.Count);
        }

        private void SubscribeToEvents()
        {
            // Подписываемся на события сделок
            var tradeSubscription = _subscriptionManager.Subscribe<TradeExecutedEvent>(
                "trade:executed",
                async (tradeEvent, ct) =>
                {
                    // Находим соответствующий тикер и добавляем сделку в его backend
                    var ticker = _tickerManager.GetTicker(tradeEvent.Symbol);
                    if (ticker != null && ticker is EventHubTicker eventHubTicker)
                    {
                        eventHubTicker.ProcessTradeEvent(tradeEvent);

                        _logger.LogDebug("Trade added to chart backend: {Symbol} {Side} @ {Price}",
                            tradeEvent.Symbol, tradeEvent.Side, tradeEvent.Price);
                    }
                    else
                    {
                        _logger.LogWarning("No ticker found for symbol {Symbol}", tradeEvent.Symbol);
                    }
                });

            _subscriptions.Add(tradeSubscription);
            _logger.LogInformation("Chart backend subscribed to trade:executed events");
        }

        // Простые геттеры
        public TickerChartAdapter this[int index] => _chartAdapters[index];
        public int Count => _chartAdapters.Count;
        public List<TickerChartAdapter> ChartAdapters => _chartAdapters;

        /// <summary>
        /// Обновление всех графиков (генерирует новые котировки)
        /// </summary>
        public void UpdateAllCharts()
        {
            // Генерируем новые котировки для всех тикеров
            // Это автоматически добавит их в соответствующие ChartBackend
            foreach (var ticker in _tickerManager.GetAllTickers())
            {
                ticker.GenerateNextQuote();
            }
        }

        /// <summary>
        /// Рендер всех адаптеров
        /// </summary>
        public void RenderAll(RazorChartViewer[] viewers)
        {
            if (viewers.Length != _chartAdapters.Count)
                throw new ArgumentException("Количество viewers должно совпадать с количеством графиков");

            for (int i = 0; i < _chartAdapters.Count; i++)
            {
                _chartAdapters[i].Render(viewers[i]);
            }
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();

            _logger.LogInformation("ChartContainer disposed");
        }
    }
}
