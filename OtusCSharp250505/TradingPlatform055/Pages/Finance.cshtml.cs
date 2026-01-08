// Pages/FinanceModel.cshtml.cs
using ChartDirector;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TradingPlatform.Charts;

namespace TradingPlatform.Pages
{
    public class FinanceModel : PageModel
    {
        private readonly ChartContainer _chartContainer;
        private readonly int _windowsCount;

        public FinanceModel(ChartContainer chartContainer)
        {
            _chartContainer = chartContainer ?? throw new ArgumentNullException(nameof(chartContainer));
            _windowsCount = _chartContainer.Count;

            Console.WriteLine($"FinanceModel: Инициализировано с {_windowsCount} графиками");
        }

        public void OnGet()
        {
            ViewData["Title"] = "Торговые графики";

            var viewers = new RazorChartViewer[_windowsCount];
            ViewData["Viewer"] = viewers;

            for (int i = 0; i < _windowsCount; ++i)
            {
                viewers[i] = new RazorChartViewer(HttpContext, $"chart{i}");
                _chartContainer[i].Render(viewers[i]);
            }

            Console.WriteLine($"FinanceModel.OnGet(): Отрисовано {_windowsCount} графиков");
        }

        public IActionResult OnGetChartHtml()
        {
            Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");

            var htmlParts = new List<string>();
            for (int i = 0; i < _chartContainer.Count; i++)
            {
                var viewer = new RazorChartViewer(HttpContext, $"chart{i}");
                // Добавляем новую котировку и отрисовываем
               // _chartContainer[i].AddNextQuote(viewer);
                _chartContainer[i].Render(viewer);
                htmlParts.Add(viewer.RenderHTML());
            }

            return Content(string.Join("", htmlParts), "text/html; charset=utf-8");
        }
    }
}
