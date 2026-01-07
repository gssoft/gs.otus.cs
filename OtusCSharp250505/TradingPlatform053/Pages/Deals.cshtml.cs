// Pages/Deals.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TradingPlatform.Visualization;
using TradingPlatform.Services;

namespace TradingPlatform.Pages
{
    public class DealsModel : PageModel
    {
        private readonly IInMemoryTradingDatabase _database;

        [BindProperty(SupportsGet = true)]
        public string? SelectedTicker { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedStrategy { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public const int PageSize = 50;

        public DealsModel(IInMemoryTradingDatabase database)
        {
            _database = database;
        }

        public PagedResult<VisualDeal> Deals { get; set; } = new();

        public void OnGet()
        {
            Deals = _database.GetDeals(SelectedTicker, SelectedStrategy, PageNumber, PageSize);
        }
    }
}


//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.RazorPages;
//using TradingPlatform.Visualization;
//using TradingPlatform.Services;

//namespace TradingPlatform.Pages
//{
//    public class DealsModel : PageModel
//    {
//        private readonly IInMemoryTradingDatabase _database;

//        [BindProperty(SupportsGet = true)]
//        public string? SelectedTicker { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public string? SelectedStrategy { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public int PageNumber { get; set; } = 1;

//        public const int PageSize = 50;

//        public DealsModel(IInMemoryTradingDatabase database)
//        {
//            _database = database;
//        }

//        public PagedResult<VisualDeal> Deals { get; set; } = new();

//        public void OnGet()
//        {
//            Deals = _database.GetDeals(SelectedTicker, SelectedStrategy, PageNumber, PageSize);
//        }
//    }
//}
