// Charts/TickerChartAdapter.cs

using ChartDirector;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core;

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

        public void Render(RazorChartViewer viewer)
        {
            lock (_renderLock)
            {
                try
                {
                  //  _logger.LogDebug("Starting render for {Symbol}", _ticker.Symbol);

                    var (timeStamps, highData, lowData, openData, closeData, volData) = _ticker.GetRawData();

                 //   _logger.LogDebug("Data retrieved: TimeStamps={TimeStampsCount}, CloseData={CloseDataCount}",
                 //       timeStamps.Length, closeData.Length);

                    RenderChart1(viewer, timeStamps, highData, lowData, openData, closeData, volData, _ticker.Symbol);

                //    _logger.LogDebug("Render completed for {Symbol}", _ticker.Symbol);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rendering chart for {Symbol}", _ticker.Symbol);
                    viewer.Image = null;
                }
            }
        }

        private void RenderChart(RazorChartViewer viewer,
            double[] timeStamps, double[] highData, double[] lowData,
            double[] openData, double[] closeData, double[] volData)
        {
            try
            {
                // _logger.LogDebug("Creating FinanceChart for {Count} data points", timeStamps.Length);

                if (timeStamps.Length == 0)
                {
                    _logger.LogWarning("No data available for {Symbol}", _ticker.Symbol);
                    viewer.Image = null;
                    return;
                }

                // Проверяем данные перед передачей в ChartDirector
                ValidateData(timeStamps, highData, lowData, openData, closeData, volData);

                FinanceChart c = new FinanceChart(420);
                c.setMargins(0, 0, 40, 0);

                int extraDays = 30;

              //  _logger.LogDebug("Setting data to FinanceChart (extraDays={ExtraDays})", extraDays);
                c.setData(timeStamps, highData, lowData, openData, closeData, volData, extraDays);



                // Добавляем индикаторы
                c.addSlowStochastic(75, 14, 3, 0x006060, 0x606000);
                c.addMainChart(240);
                c.addSimpleMovingAvg(10, 0x663300);
                c.addSimpleMovingAvg(20, 0x9900ff);
                c.addCandleStick(0x00ff00, 0xff0000);
                c.addDonchianChannel(14, 0x9999ff, unchecked((int)0xc06666ff));
                c.addVolIndicator(75, 0x00ff00, 0xff0000, 0x808080);
                c.addMACD(75, 26, 12, 9, 0x0000ff, 0xff00ff, 0x008000);

              //  _logger.LogDebug("Generating chart image for {Symbol}", _ticker.Symbol);
                viewer.Image = c.makeWebImage(ChartDirector.Chart.SVG);

              //  _logger.LogInformation("✅ Chart rendered successfully for {Symbol}", _ticker.Symbol);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "ArgumentException in ChartDirector for {Symbol}", _ticker.Symbol);
                _logger.LogError("Data lengths - TimeStamps: {t}, High: {h}, Low: {l}, Open: {o}, Close: {c}, Vol: {v}",
                    timeStamps.Length, highData.Length, lowData.Length,
                    openData.Length, closeData.Length, volData.Length);
                viewer.Image = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating chart for {Symbol}", _ticker.Symbol);
                viewer.Image = null;
            }
        }
        private void RenderChart1(RazorChartViewer viewer,
            double[] timeStamps, double[] highData, double[] lowData,
            double[] openData, double[] closeData, double[] volData, string symbol)
        {
            try
            {
                // _logger.LogDebug("Creating FinanceChart for {Count} data points", timeStamps.Length);

                if (timeStamps.Length == 0)
                {
                    _logger.LogWarning("No data available for {Symbol}", _ticker.Symbol);
                    viewer.Image = null;
                    return;
                }

                // Проверяем данные перед передачей в ChartDirector
                ValidateData(timeStamps, highData, lowData, openData, closeData, volData);

                FinanceChart c = new FinanceChart(420);
                
                // c.addTitle(symbol);
                c.setMargins(0, 0, 40, 0);
                // c.addTitle2(5, symbol, "Arial Bold", 26);
                // c.addTitle2(5, symbol, "Arial Bold", 26, 0x0000FF);
                // c.addTitle2(5, symbol, "Arial Bold", 26, unchecked((int)0x800000FF));
                
                int extraDays = 30;

                //  _logger.LogDebug("Setting data to FinanceChart (extraDays={ExtraDays})", extraDays);
                c.setData(timeStamps, highData, lowData, openData, closeData, volData, extraDays);

                // Добавляем индикаторы
                c.addSlowStochastic(75, 14, 3, 0x006060, 0x606000);
                c.addMainChart(240);
                
                // ******************************************************************
                c.addTitle2(7, symbol, "Arial Bold", 42, unchecked((int)0x706666ff));
                // c.addTitle2(7, symbol, "Arial Bold", 50, unchecked((int)0x880000ff));

                c.addSimpleMovingAvg(10, 0x663300);
                c.addSimpleMovingAvg(20, 0x9900ff);
                c.addCandleStick(0x00ff00, 0xff0000);
                c.addDonchianChannel(14, 0x9999ff, unchecked((int)0xc06666ff));
                c.addVolIndicator(75, 0x00ff00, 0xff0000, 0x808080);
                c.addMACD(75, 26, 12, 9, 0x0000ff, 0xff00ff, 0x008000);

                //  _logger.LogDebug("Generating chart image for {Symbol}", _ticker.Symbol);
                viewer.Image = c.makeWebImage(ChartDirector.Chart.SVG);

                //  _logger.LogInformation("✅ Chart rendered successfully for {Symbol}", _ticker.Symbol);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "ArgumentException in ChartDirector for {Symbol}", _ticker.Symbol);
                _logger.LogError("Data lengths - TimeStamps: {t}, High: {h}, Low: {l}, Open: {o}, Close: {c}, Vol: {v}",
                    timeStamps.Length, highData.Length, lowData.Length,
                    openData.Length, closeData.Length, volData.Length);
                viewer.Image = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating chart for {Symbol}", _ticker.Symbol);
                viewer.Image = null;
            }
        }
        private void ValidateData(
            double[] timeStamps, double[] highData, double[] lowData,
            double[] openData, double[] closeData, double[] volData)
        {
            // Проверяем, что все массивы имеют одинаковую длину
            int expectedLength = timeStamps.Length;

            if (highData.Length != expectedLength ||
                lowData.Length != expectedLength ||
                openData.Length != expectedLength ||
                closeData.Length != expectedLength ||
                volData.Length != expectedLength)
            {
                throw new ArgumentException($"Data arrays have different lengths: " +
                    $"TimeStamps={timeStamps.Length}, High={highData.Length}, " +
                    $"Low={lowData.Length}, Open={openData.Length}, " +
                    $"Close={closeData.Length}, Vol={volData.Length}");
            }

            // Проверяем, что данные не содержат NaN или бесконечных значений
            for (int i = 0; i < expectedLength; i++)
            {
                if (double.IsNaN(timeStamps[i]) || double.IsInfinity(timeStamps[i]))
                    throw new ArgumentException($"Invalid timeStamp at index {i}: {timeStamps[i]}");

                if (double.IsNaN(closeData[i]) || double.IsInfinity(closeData[i]))
                    throw new ArgumentException($"Invalid closeData at index {i}: {closeData[i]}");
            }

            // _logger.LogDebug("Data validation passed for {ExpectedLength} points", expectedLength);
        }
    }
}