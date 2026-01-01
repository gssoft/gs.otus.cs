// Hubs/TradingDataHub.cs
using Microsoft.AspNetCore.SignalR;
using TradingPlatform.Services;
using TradingPlatform.Visualization;

namespace TradingPlatform.Hubs
{
    public class TradingDataHub : Hub
    {
        private readonly IInMemoryTradingDatabase _database;
        private readonly ILogger<TradingDataHub> _logger;

        public TradingDataHub(
            IInMemoryTradingDatabase database,
            ILogger<TradingDataHub> logger)
        {
            _database = database;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

            // Подписка на события обновлений
            _database.SummaryUpdated += OnSummaryUpdated;
            _database.TradeAdded += OnTradeAdded;
            _database.DealAdded += OnDealAdded;
            _database.OrderAdded += OnOrderAdded;

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

            // Отписка от событий
            _database.SummaryUpdated -= OnSummaryUpdated;
            _database.TradeAdded -= OnTradeAdded;
            _database.DealAdded -= OnDealAdded;
            _database.OrderAdded -= OnOrderAdded;

            await base.OnDisconnectedAsync(exception);
        }

        private async void OnSummaryUpdated(TickerStrategySummary summary)
        {
            await Clients.All.SendAsync("UpdateSummary", summary);
        }

        private async void OnTradeAdded(VisualTrade trade)
        {
            await Clients.All.SendAsync("AddTrade", trade);
        }

        private async void OnDealAdded(VisualDeal deal)
        {
            await Clients.All.SendAsync("AddDeal", deal);
        }

        private async void OnOrderAdded(VisualOrder order)
        {
            await Clients.All.SendAsync("AddOrder", order);
        }

        // Методы для запросов от клиента
        public async Task<IEnumerable<TickerStrategySummary>> GetSummaries()
        {
            return _database.GetSummaries();
        }

        public async Task<PagedResult<VisualTrade>> GetTrades(string? ticker, string? strategy, int page)
        {
            return _database.GetTrades(ticker, strategy, page, 20);
        }

        public async Task<PagedResult<VisualDeal>> GetDeals(string? ticker, string? strategy, int page)
        {
            return _database.GetDeals(ticker, strategy, page, 20);
        }

        public async Task<PagedResult<VisualOrder>> GetOrders(string? ticker, string? strategy, int page)
        {
            return _database.GetOrders(ticker, strategy, page, 20);
        }
    }
}
