// Pages/Summary.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TradingPlatform.Visualization;
using TradingPlatform.Services;

namespace TradingPlatform.Pages
{
    public class SummaryModel : PageModel
    {
        private readonly IInMemoryTradingDatabase _database;

        [BindProperty(SupportsGet = true)]
        public string? SelectedTicker { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedStrategy { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public const int PageSize = 50;

        public SummaryModel(IInMemoryTradingDatabase database)
        {
            _database = database;
        }

        public List<TickerStrategySummary> Summaries { get; set; } = new();

        public void OnGet()
        {
            var allSummaries = _database.GetSummaries();

            // Фильтрация
            var filteredSummaries = allSummaries.AsEnumerable();

            if (!string.IsNullOrEmpty(SelectedTicker))
                filteredSummaries = filteredSummaries.Where(s => s.Ticker == SelectedTicker);

            if (!string.IsNullOrEmpty(SelectedStrategy))
                filteredSummaries = filteredSummaries.Where(s => s.Strategy == SelectedStrategy);

            var totalCount = filteredSummaries.Count();

            // Пагинация
            Summaries = filteredSummaries
                .OrderBy(s => s.Ticker)
                .ThenBy(s => s.Strategy)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            TotalCount = totalCount;
            TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);

            // Вычисляем дополнительные статистики
            TotalOpenPnL = Summaries.Sum(s => s.OpenPnL);
            TotalClosedPnL = Summaries.Sum(s => s.ClosedPnL);
            TotalPnL = TotalOpenPnL + TotalClosedPnL;
            OpenPositions = Summaries.Count(s => s.Position != 0);
            ClosedPositions = Summaries.Count(s => s.Position == 0);
            ActiveStrategies = OpenPositions;
            AveragePnL = Summaries.Count > 0 ? TotalPnL / Summaries.Count : 0;
        }

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        // Дополнительные статистики
        public decimal TotalOpenPnL { get; set; }
        public decimal TotalClosedPnL { get; set; }
        public decimal TotalPnL { get; set; }
        public int OpenPositions { get; set; }
        public int ClosedPositions { get; set; }
        public int ActiveStrategies { get; set; }
        public decimal AveragePnL { get; set; }
    }
}

