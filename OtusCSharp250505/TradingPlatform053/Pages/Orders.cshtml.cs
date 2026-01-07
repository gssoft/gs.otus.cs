// Pages/Orders.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TradingPlatform.Visualization;
using TradingPlatform.Services;

namespace TradingPlatform.Pages
{
    public class OrdersModel : PageModel
    {
        private readonly IInMemoryTradingDatabase _database;

        [BindProperty(SupportsGet = true)]
        public string? SelectedTicker { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedStatus { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public const int PageSize = 50;

        public OrdersModel(IInMemoryTradingDatabase database)
        {
            _database = database;
        }

        public PagedResult<VisualOrder> Orders { get; set; } = new();

        public void OnGet()
        {
            // Используем SelectedStatus как фильтр по стратегии (в базе стратегия хранится как статус)
            Orders = _database.GetOrders(SelectedTicker, SelectedStatus, PageNumber, PageSize);
        }
    }
}




//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.RazorPages;
//using TradingPlatform.Visualization;
//using TradingPlatform.Services;

//namespace TradingPlatform.Pages
//{
//    public class OrdersModel : PageModel
//    {
//        private readonly IInMemoryTradingDatabase _database;

//        [BindProperty(SupportsGet = true)]
//        public string? SelectedTicker { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public string? SelectedStrategy { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public int PageNumber { get; set; } = 1;

//        public const int PageSize = 50;

//        public OrdersModel(IInMemoryTradingDatabase database)
//        {
//            _database = database;
//        }

//        public PagedResult<VisualOrder> Orders { get; set; } = new();

//        public void OnGet()
//        {
//            Orders = _database.GetOrders(SelectedTicker, SelectedStrategy, PageNumber, PageSize);
//        }
//    }
//}
