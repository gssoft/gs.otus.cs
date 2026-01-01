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
        private readonly IEventHub _eventHub;
        private readonly IDynamicSubscriptionManager _subscriptionManager;
        private readonly ILogger<ChartContainer> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly List<IDisposable> _subscriptions = new();

        public ChartContainer(
            EventHubTickerManager tickerManager,
            IEventHub eventHub,
            IDynamicSubscriptionManager subscriptionManager,
            ILogger<ChartContainer> logger,
            ILoggerFactory loggerFactory)
        {
            _tickerManager = tickerManager;
            _eventHub = eventHub;
            _subscriptionManager = subscriptionManager;
            _logger = logger;
            _loggerFactory = loggerFactory;

            InitializeChartAdapters();
            SubscribeToEvents();
        }

        private void InitializeChartAdapters()
        {
            foreach (var ticker in _tickerManager.GetAllTickers())
            {
                _chartAdapters.Add(new TickerChartAdapter(ticker,
                    _loggerFactory.CreateLogger<TickerChartAdapter>()));
            }

            _logger.LogInformation("ChartContainer initialized with {Count} chart adapters", _chartAdapters.Count);
        }

        private void SubscribeToEvents()
        {
            // Подписываемся на события обновления графиков
            var subscription = _subscriptionManager.Subscribe<ChartUpdateEvent>(
                "chart:update:*",
                async (chartEvent, ct) =>
                {
                    _logger.LogDebug("Chart update event for {Symbol}", chartEvent.Symbol);
                    
                });

            _subscriptions.Add(subscription);
        }

        public TickerChartAdapter this[int index] => _chartAdapters[index];
        public int Count => _chartAdapters.Count;
        public List<TickerChartAdapter> ChartAdapters => _chartAdapters;

        public void UpdateAllCharts()
        {
            _logger.LogDebug("Updating all charts");
            // Генерируем новые котировки для всех тикеров
            foreach (var ticker in _tickerManager.GetAllTickers())
            {
                ticker.GenerateNextQuote();
            }
        }

        public void RenderAll(RazorChartViewer[] viewers)
        {
            if (viewers.Length != _chartAdapters.Count)
                throw new ArgumentException("Количество viewers должно совпадать с количеством графиков");

            for (int i = 0; i < _chartAdapters.Count; i++)
            {
                _chartAdapters[i].Render(viewers[i]);
            }

          //  _logger.LogDebug("Rendered {Count} charts", _chartAdapters.Count);
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