// Pages/Trades.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TradingPlatform.Visualization;
using TradingPlatform.Services;

namespace TradingPlatform.Pages
{
    public class TradesModel : PageModel
    {
        private readonly IInMemoryTradingDatabase _database;

        [BindProperty(SupportsGet = true)]
        public string? SelectedTicker { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedStrategy { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public const int PageSize = 50;

        public TradesModel(IInMemoryTradingDatabase database)
        {
            _database = database;
        }

        public PagedResult<VisualTrade> Trades { get; set; } = new();

        public void OnGet()
        {
            Trades = _database.GetTrades(SelectedTicker, SelectedStrategy, PageNumber, PageSize);
        }
    }
}