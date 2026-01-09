// Core/QuoteEngine.cs

using ChartDirector;
using Microsoft.Extensions.Logging;

namespace TradingPlatform.Core;

public class QuoteEngine
{
    // public event Action? DataShifted; //  событие

    private bool _isInitialized = false;
    private readonly object _lockObject = new();
    private int _generationCount = 0;
    private readonly ILogger<QuoteEngine> _logger;

    // Поля для хранения данных
    private double[] _timeStamps = [];
    private double[] _highData = [];
    private double[] _lowData = [];
    private double[] _openData = [];
    private double[] _closeData = [];
    private double[] _volData = [];

    // Публичные свойства
    public double[] TimeStamps => _timeStamps;
    public double[] HighData => _highData;
    public double[] LowData => _lowData;
    public double[] OpenData => _openData;
    public double[] CloseData => _closeData;
    public double[] VolData => _volData;

    public int LastIndex => _timeStamps.Length - 1;
    public int GenerationCount => _generationCount;

    // Параметры генерации
    private readonly int _extraDays = 30;
    private readonly int _randomSeed;
    private readonly int _startYear = 2002;
    private readonly int _startMonth = 9;
    private readonly int _startDay = 4;
    private readonly int _noOfDays = 80;
    private readonly int _minDelta = -14;
    private readonly int _maxDelta = 14;
    private readonly bool _startFromEmptyArray = false;
    private readonly decimal _initialPrice;

    public QuoteEngine(int randomSeed, int noOfDays = 80, decimal initialPrice = 1000m, ILogger<QuoteEngine> logger = null)
    {
        _randomSeed = randomSeed;
        _noOfDays = noOfDays;
        _initialPrice = initialPrice;
        _logger = logger;

        _logger?.LogDebug("QuoteEngine создан: seed={Seed}, days={Days}, initialPrice={InitialPrice}",
            _randomSeed, _noOfDays, _initialPrice);
    }

    public void InitializeQuotes()
    {
        lock (_lockObject)
        {
            if (!_isInitialized)
            {
                _logger?.LogInformation("Initializing QuoteEngine {Seed}", _randomSeed);

                RanTable rantable = new RanTable(_randomSeed, 6, _noOfDays + _extraDays);

                // Генерируем цены и объем
                rantable.setHLOCCols(1, (double)_initialPrice, _minDelta, _maxDelta);
                rantable.setCol(5, 50_000_000, 250_000_000);

                if (_startFromEmptyArray)
                {
                    _timeStamps = new double[_noOfDays];
                    _highData = new double[_noOfDays];
                    _lowData = new double[_noOfDays];
                    _openData = new double[_noOfDays];
                    _closeData = new double[_noOfDays];
                    _volData = new double[_noOfDays];
                }
                else
                {
                    // Получаем данные цен и объема
                    _highData = rantable.getCol(1);
                    _lowData = rantable.getCol(2);
                    _openData = rantable.getCol(3);
                    _closeData = rantable.getCol(4);
                    _volData = rantable.getCol(5);

                    // Генерируем временные метки самостоятельно
                    // ВАЖНО: не используем getCol(0) из RanTable!
                    DateTime startDate = new DateTime(_startYear, _startMonth, _startDay);
                    _timeStamps = GenerateTimestamps(_highData.Length, startDate);

                    _logger?.LogDebug("Generated {Count} timestamps from {StartDate}",
                        _timeStamps.Length, startDate.ToString("yyyy-MM-dd"));
                }

                _isInitialized = true;
                _logger?.LogInformation("✅ QuoteEngine {Seed} initialized. Data points: {Count}",
                    _randomSeed, _closeData.Length);
            }
        }
    }

    // Метод для генерации корректных временных меток
    private double[] GenerateTimestamps(int count, DateTime startDate)
    {
        var timestamps = new double[count];
        for (int i = 0; i < count; i++)
        {
            // Генерируем корректные OLE Automation Dates
            DateTime currentDate = startDate.AddDays(i);
            timestamps[i] = currentDate.ToOADate();
        }
        return timestamps;
    }

    public Quote GetCurrentQuote()
    {
        lock (_lockObject)
        {
            if (!_isInitialized || _closeData.Length == 0)
                throw new InvalidOperationException($"QuoteEngine[{_randomSeed}] not initialized");

            int lastIndex = _closeData.Length - 1;

            // Получаем временную метку
            double timestamp = _timeStamps[lastIndex];

            DateTime dateTime;

            // Проверяем корректность OLE Automation Date
            if (IsValidOADate(timestamp))
            {
                try
                {
                    dateTime = DateTime.FromOADate(timestamp);
                }
                catch (ArgumentException)
                {
                    // Если преобразование не удалось, используем текущее время
                    _logger?.LogWarning("Invalid OLE Date: {Timestamp}, using current time", timestamp);
                    dateTime = DateTime.Now;
                }
            }
            else
            {
                // Если timestamp некорректен, используем текущее время
                _logger?.LogWarning("Invalid OLE Date format: {Timestamp}, using current time", timestamp);
                dateTime = DateTime.Now;
            }

            return new Quote
            {
                Timestamp = dateTime,
                Open = (decimal)_openData[lastIndex],
                High = (decimal)_highData[lastIndex],
                Low = (decimal)_lowData[lastIndex],
                Close = (decimal)_closeData[lastIndex],
                Volume = (long)_volData[lastIndex]
            };
        }
    }

    public List<Quote> GetQuoteHistory(int count = -1)
    {
        lock (_lockObject)
        {
            if (!_isInitialized)
                throw new InvalidOperationException($"QuoteEngine[{_randomSeed}] not initialized");

            var quotes = new List<Quote>();
            int totalCount = _timeStamps.Length;
            int startIndex = count <= 0 || count >= totalCount ? 0 : totalCount - count;

            for (int i = startIndex; i < totalCount; i++)
            {
                DateTime timestamp;
                double oaDate = _timeStamps[i];

                if (IsValidOADate(oaDate))
                {
                    try
                    {
                        timestamp = DateTime.FromOADate(oaDate);
                    }
                    catch (ArgumentException)
                    {
                        // Если преобразование не удалось, используем текущее время с отступом
                        timestamp = DateTime.Now.AddDays(-(totalCount - i));
                    }
                }
                else
                {
                    // Если timestamp некорректен, используем текущее время с отступом
                    timestamp = DateTime.Now.AddDays(-(totalCount - i));
                }

                quotes.Add(new Quote
                {
                    Timestamp = timestamp,
                    Open = (decimal)_openData[i],
                    High = (decimal)_highData[i],
                    Low = (decimal)_lowData[i],
                    Close = (decimal)_closeData[i],
                    Volume = (long)_volData[i]
                });
            }

            return quotes;
        }
    }

    public void GenerateNextQuote()
    {
        lock (_lockObject)
        {
            if (!_isInitialized)
            {
                InitializeQuotes();
                return;
            }

            _generationCount++;
            int generationSeed = _randomSeed + _generationCount;

            RanTable nextRow = new RanTable(generationSeed, 6, 1);

            // Генерируем следующую дату: последняя дата + 1 день
            double lastTimestamp = _timeStamps.Last();
            DateTime lastDate;

            if (IsValidOADate(lastTimestamp))
            {
                try
                {
                    lastDate = DateTime.FromOADate(lastTimestamp);
                }
                catch (ArgumentException)
                {
                    // Если последняя временная метка некорректен, используем текущую дату
                    _logger?.LogWarning("Invalid last timestamp: {Timestamp}, using current date", lastTimestamp);
                    lastDate = DateTime.Now;
                }
            }
            else
            {
                // Если последняя временная метка некорректен, используем текущую дату
                _logger?.LogWarning("Invalid last timestamp format: {Timestamp}, using current date", lastTimestamp);
                lastDate = DateTime.Now;
            }

            DateTime nextDate = lastDate.AddDays(1);
            double nextTimestamp = nextDate.ToOADate();

            // Устанавливаем дату для nextRow (для внутренней логики RanTable)
            nextRow.setDateCol(0, nextDate, 86400, true);
            nextRow.setHLOCCols(1, _closeData.Last(), _minDelta, _maxDelta);
            nextRow.setCol(5, 50_000_000, 250_000_000);

            double[] newHigh = nextRow.getCol(1);
            double[] newLow = nextRow.getCol(2);
            double[] newOpen = nextRow.getCol(3);
            double[] newClose = nextRow.getCol(4);
            double[] newVolume = nextRow.getCol(5);

            // Сдвигаем массивы и добавляем новые значения
            ShiftAndAppend(ref _timeStamps, nextTimestamp); // Используем сгенерированную нами дату
            ShiftAndAppend(ref _highData, newHigh[0]);
            ShiftAndAppend(ref _lowData, newLow[0]);
            ShiftAndAppend(ref _openData, newOpen[0]);
            ShiftAndAppend(ref _closeData, newClose[0]);
            ShiftAndAppend(ref _volData, newVolume[0]);

            _logger?.LogDebug("Generated next quote #{Count}: {Symbol} @ {Close:F2}",
                _generationCount, _initialPrice, newClose[0]);
        }
    }

    private void ShiftAndAppend(ref double[] array, double newValue)
    {
        // Найдем индекс первой незаполненной (нулевой) ячейки
        int firstZeroIndex = -1;
        for (int i = 0; i < array.Length; i++)
        {
            if (Math.Abs(array[i]) < 0.000001) // Проверяем на ноль с учетом точности double
            {
                firstZeroIndex = i;
                break;
            }
        }

        // Если все ячейки заняты, делаем обычный сдвиг
        if (firstZeroIndex == -1)
        {
            double[] newArray = new double[array.Length];
            Array.Copy(array, 1, newArray, 0, array.Length - 1);
            newArray[newArray.Length - 1] = newValue;
            array = newArray;
        }
        else
        {
            // Иначе устанавливаем значение в первую пустую ячейку
            array[firstZeroIndex] = newValue;
        }
    }

    // Метод для проверки корректности OLE Automation Date
    private bool IsValidOADate(double oaDate)
    {
        // Допустимый диапазон OLE Automation Date:
        // Минимум: -657435.0 (1 января 100 года)
        // Максимум: 2958466.0 (31 декабря 9999 года)
        // NaN и Infinity недопустимы

        if (double.IsNaN(oaDate) || double.IsInfinity(oaDate))
            return false;

        // Проверяем, что дата в допустимом диапазоне
        const double minValidOADate = -657435.0;
        const double maxValidOADate = 2958466.0;

        return oaDate >= minValidOADate && oaDate <= maxValidOADate;
    }

    public void ResetQuotes()
    {
        lock (_lockObject)
        {
            _isInitialized = false;
            _generationCount = 0;
            InitializeQuotes();
        }
    }

    // Метод для получения сырых данных для ChartDirector
    public (double[] timeStamps, double[] highData, double[] lowData,
            double[] openData, double[] closeData, double[] volData) GetChartDirectorData()
    {
        lock (_lockObject)
        {
            if (!_isInitialized)
            {
                _logger?.LogWarning("QuoteEngine not initialized, initializing now");
                InitializeQuotes();
            }

            return (_timeStamps, _highData, _lowData, _openData, _closeData, _volData);
        }
    }

    public string GetStats()
    {
        lock (_lockObject)
        {
            if (!_isInitialized || _closeData.Length == 0)
                return $"QuoteEngine[{_randomSeed}]: Not initialized";

            return $"QuoteEngine[{_randomSeed}]: Generated {_generationCount} quotes, " +
                    $"Current Close: {_closeData.Last():F2}, " +
                    $"Min: {_closeData.Min():F2}, " +
                    $"Max: {_closeData.Max():F2}";
        }
    }
}
