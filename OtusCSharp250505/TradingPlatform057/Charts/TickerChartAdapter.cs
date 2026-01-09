// Charts/TickerChartAdapter.cs
using ChartDirector;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core;
using TradingPlatform.Events;

namespace TradingPlatform.Charts
{
    public class TickerChartAdapter
    {
        private readonly Ticker _ticker;
        private readonly object _renderLock = new();
        private readonly ILogger<TickerChartAdapter> _logger;

        public TickerChartAdapter(Ticker ticker, ILogger<TickerChartAdapter> logger)
        {
            _ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
            _logger = logger;
        }

        /// <summary>
        /// Просто рендерим готовый снимок данных
        /// </summary>
        public void Render(RazorChartViewer viewer)
        {
            lock (_renderLock)
            {
                try
                {
                    // Получаем готовый снимок всех данных
                    var snapshot = _ticker.GetChartData();

                    if (snapshot.Count == 0)
                    {
                        _logger.LogWarning("No data available for {Symbol}", _ticker.Symbol);
                        viewer.Image = null;
                        return;
                    }

                    RenderChart(viewer, snapshot);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rendering chart for {Symbol}", _ticker.Symbol);
                    viewer.Image = null;
                }
            }
        }

        private void RenderChart(RazorChartViewer viewer, ChartDataSnapshot snapshot)
        {
            try
            {
                FinanceChart c = new FinanceChart(420);
                c.setMargins(0, 0, 40, 0);

                int extraDays = 30;
                c.setData(snapshot.TimeStamps, snapshot.HighData, snapshot.LowData,
                         snapshot.OpenData, snapshot.CloseData, snapshot.VolData, extraDays);

                // Добавляем индикаторы
                c.addSlowStochastic(75, 14, 3, 0x006060, 0x606000);

                // Получаем основной график
                XYChart mainChart = c.addMainChart(240);

                c.addTitle2(7, _ticker.Symbol, "Arial Bold", 42, unchecked((int)0x706666ff));
                c.addSimpleMovingAvg(10, 0x663300);
                c.addSimpleMovingAvg(20, 0x9900ff);
                c.addCandleStick(0x00ff00, 0xff0000);
                c.addDonchianChannel(14, 0x9999ff, unchecked((int)0xc06666ff));
                c.addVolIndicator(75, 0x00ff00, 0xff0000, 0x808080);
                c.addMACD(75, 26, 12, 9, 0x0000ff, 0xff00ff, 0x008000);

                // Добавляем стрелки для сделок (если есть)
                AddTradeArrowsToChart(mainChart, snapshot.BuySignals, snapshot.SellSignals);

                viewer.Image = c.makeWebImage(Chart.SVG);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chart for {Symbol}", _ticker.Symbol);
                viewer.Image = null;
            }
        }

        private void AddTradeArrowsToChart(XYChart chart, double[] buySignals, double[] sellSignals)
        {
            bool hasBuySignals = buySignals.Any(s => s != Chart.NoValue);
            bool hasSellSignals = sellSignals.Any(s => s != Chart.NoValue);

            if (!hasBuySignals && !hasSellSignals) return;

            // Добавляем слой для покупок
            if (hasBuySignals)
            {
                ScatterLayer buyLayer = chart.addScatterLayer(
                    null,
                    buySignals,
                    "Buy",
                    Chart.ArrowShape(0, 1, 0.4, 0.4),
                    13,
                    0x00FF00
                );

                if (buyLayer?.getDataSet(0) != null)
                {
                    buyLayer.getDataSet(0).setSymbolOffset(0, -7);
                }
            }

            // Добавляем слой для продаж
            if (hasSellSignals)
            {
                ScatterLayer sellLayer = chart.addScatterLayer(
                    null,
                    sellSignals,
                    "Sell",
                    Chart.ArrowShape(180, 1, 0.4, 0.4),
                    13,
                    0xFF0000
                );

                if (sellLayer?.getDataSet(0) != null)
                {
                    sellLayer.getDataSet(0).setSymbolOffset(0, 7);
                }
            }
        }

        public string TickerSymbol => _ticker.Symbol;
    }
}
