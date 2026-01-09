// Pages/Logs.cshtml.cs
// Pages/Logs.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TradingPlatform.Services;
using TradingPlatform.Visualization;

namespace TradingPlatform.Pages
{
    public class LogsModel : PageModel
    {
        private readonly IInMemoryLogDatabase _logDatabase;

        [BindProperty(SupportsGet = true)]
        public string? SelectedTicker { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedStrategy { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedLevel { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedCategory { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public const int PageSize = 50;

        public LogsModel(IInMemoryLogDatabase logDatabase)
        {
            _logDatabase = logDatabase;
        }

        public PagedResult<TradingLog> Logs { get; set; } = new();

        public void OnGet()
        {
            Logs = _logDatabase.GetPagedLogs(
                SelectedTicker,
                SelectedStrategy,
                SelectedLevel,
                SelectedCategory,
                PageNumber,
                PageSize);
        }
    }
}


//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.RazorPages;
//using TradingPlatform.Services;
//using TradingPlatform.Visualization;

//namespace TradingPlatform.Pages
//{
//    public class LogsModel : PageModel
//    {
//        private readonly IInMemoryLogDatabase _logDatabase;

//        [BindProperty(SupportsGet = true)]
//        public string? SelectedTicker { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public string? SelectedStrategy { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public string? SelectedLevel { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public string? SelectedCategory { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public int PageNumber { get; set; } = 1;

//        public const int PageSize = 50;

//        public LogsModel(IInMemoryLogDatabase logDatabase)
//        {
//            _logDatabase = logDatabase;
//        }

//        public PagedResult<TradingLog> Logs { get; set; } = new();

//        public void OnGet()
//        {
//            Logs = _logDatabase.GetPagedLogs(
//                SelectedTicker,
//                SelectedStrategy,
//                SelectedLevel,
//                SelectedCategory,
//                PageNumber,
//                PageSize);
//        }
//    }
//}
