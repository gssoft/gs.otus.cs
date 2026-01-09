// Pages/Positions.cshtml.cs
// 26.01.01
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TradingPlatform.Visualization;
using TradingPlatform.Services;

namespace TradingPlatform.Pages
{
    public class PositionsModel : PageModel
    {
        private readonly IInMemoryTradingDatabase _database;

        [BindProperty(SupportsGet = true)]
        public string? SelectedTicker { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedStrategy { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool OnlyOpenPositions { get; set; } = true;

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public const int PageSize = 50;

        public PositionsModel(IInMemoryTradingDatabase database)
        {
            _database = database;
        }

        public List<PositionViewModel> Positions { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public decimal TotalOpenPnL { get; set; }
        public decimal TotalClosedPnL { get; set; }
        public decimal TotalPnL => TotalOpenPnL + TotalClosedPnL;
        public decimal TotalExposure { get; set; }

        public void OnGet()
        {
            var allSummaries = _database.GetSummaries();

            // Конвертируем в PositionViewModel
            var allPositions = allSummaries.Select(s => new PositionViewModel
            {
                Ticker = s.Ticker,
                Strategy = s.Strategy,
                Position = s.Position,
                OpenPnL = s.OpenPnL,
                ClosedPnL = s.ClosedPnL,
                CurrentPrice = s.CurrentPrice,
                OpenTrades = s.OpenTrades,
                TotalTrades = s.TotalTrades,
                LastUpdated = s.LastUpdated,
                IsOpenPosition = s.Position != 0
            });

            // Фильтрация
            var filteredPositions = allPositions.AsEnumerable();

            if (!string.IsNullOrEmpty(SelectedTicker))
                filteredPositions = filteredPositions.Where(p => p.Ticker == SelectedTicker);

            if (!string.IsNullOrEmpty(SelectedStrategy))
                filteredPositions = filteredPositions.Where(p => p.Strategy == SelectedStrategy);

            if (OnlyOpenPositions)
                filteredPositions = filteredPositions.Where(p => p.IsOpenPosition);

            // Сортировка: сначала открытые позиции, потом по убыванию PnL
            filteredPositions = filteredPositions
                .OrderByDescending(p => p.IsOpenPosition)
                .ThenByDescending(p => Math.Abs(p.Position))
                .ThenBy(p => p.Ticker)
                .ThenBy(p => p.Strategy);

            var totalCount = filteredPositions.Count();

            // Рассчитываем агрегаты
            TotalOpenPnL = filteredPositions.Sum(p => p.OpenPnL);
            TotalClosedPnL = filteredPositions.Sum(p => p.ClosedPnL);
            TotalExposure = filteredPositions.Where(p => p.IsOpenPosition)
                .Sum(p => Math.Abs(p.Position * p.CurrentPrice));

            // Пагинация
            Positions = filteredPositions
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            TotalCount = totalCount;
            TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);
        }

        public class PositionViewModel
        {
            public string Ticker { get; set; } = string.Empty;
            public string Strategy { get; set; } = string.Empty;
            public int Position { get; set; }
            public decimal OpenPnL { get; set; }
            public decimal ClosedPnL { get; set; }
            public decimal TotalPnL => OpenPnL + ClosedPnL;
            public int OpenTrades { get; set; }
            public int TotalTrades { get; set; }
            public decimal CurrentPrice { get; set; }
            public DateTime LastUpdated { get; set; }
            public bool IsOpenPosition { get; set; }

            // Дополнительные вычисляемые свойства
            public decimal Exposure => IsOpenPosition ? Math.Abs(Position * CurrentPrice) : 0;
            public decimal AveragePrice => CurrentPrice;
        }
    }
}

