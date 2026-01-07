// Core/ChartBackend.cs
using ChartDirector;

namespace TradingPlatform.Core
{
    /// <summary>
    /// Единый backend для хранения всех данных графика.
    /// Работает независимо от UI, всегда пополняется, сдвигается синхронно.
    /// </summary>
    public class ChartBackend
    {
        private readonly string _symbol;
        private readonly object _lock = new();

        // Конфигурация
        private const int BUFFER_SIZE = 100; // Фиксированный размер буфера

        // Основные массивы данных
        private readonly double[] _timeStamps = new double[BUFFER_SIZE];
        private readonly double[] _highData = new double[BUFFER_SIZE];
        private readonly double[] _lowData = new double[BUFFER_SIZE];
        private readonly double[] _openData = new double[BUFFER_SIZE];
        private readonly double[] _closeData = new double[BUFFER_SIZE];
        private readonly double[] _volData = new double[BUFFER_SIZE];

        // Массивы дополнительных данных (стрелки, метки, индикаторы и т.д.)
        private readonly double[] _buySignals = new double[BUFFER_SIZE];
        private readonly double[] _sellSignals = new double[BUFFER_SIZE];
        private readonly double[] _markers = new double[BUFFER_SIZE]; // Для будущих меток
        private readonly double[] _customIndicators = new double[BUFFER_SIZE]; // Для кастомных индикаторов

        // Текущая позиция в буфере
        private int _currentPos = 0;
        private int _dataCount = 0;
        private bool _bufferFilled = false;

        public event Action? DataUpdated;
        public event Action? DataShifted;

        public ChartBackend(string symbol)
        {
            _symbol = symbol;

            // Инициализируем все массивы значением NoValue
            InitializeArrays();
        }

        private void InitializeArrays()
        {
            for (int i = 0; i < BUFFER_SIZE; i++)
            {
                _timeStamps[i] = 0;
                _highData[i] = 0;
                _lowData[i] = 0;
                _openData[i] = 0;
                _closeData[i] = 0;
                _volData[i] = 0;
                _buySignals[i] = Chart.NoValue;
                _sellSignals[i] = Chart.NoValue;
                _markers[i] = Chart.NoValue;
                _customIndicators[i] = Chart.NoValue;
            }
        }

        /// <summary>
        /// Инициализация начальными данными
        /// </summary>
        public void InitializeWithData(
            double[] timeStamps,
            double[] highData,
            double[] lowData,
            double[] openData,
            double[] closeData,
            double[] volData)
        {
            lock (_lock)
            {
                int count = Math.Min(timeStamps.Length, BUFFER_SIZE);

                for (int i = 0; i < count; i++)
                {
                    _timeStamps[i] = timeStamps[i];
                    _highData[i] = highData[i];
                    _lowData[i] = lowData[i];
                    _openData[i] = openData[i];
                    _closeData[i] = closeData[i];
                    _volData[i] = volData[i];
                }

                _dataCount = count;
                _currentPos = count;
                _bufferFilled = count >= BUFFER_SIZE;

                DataUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Добавление новой котировки (всегда работает)
        /// </summary>
        public void PushQuote(
            double timestamp,
            double high,
            double low,
            double open,
            double close,
            double volume)
        {
            lock (_lock)
            {
                // Если буфер заполнен, сдвигаем ВСЕ данные
                if (_bufferFilled)
                {
                    ShiftAllArrays();
                    _currentPos = BUFFER_SIZE - 1;
                }

                // Добавляем данные в текущую позицию
                _timeStamps[_currentPos] = timestamp;
                _highData[_currentPos] = high;
                _lowData[_currentPos] = low;
                _openData[_currentPos] = open;
                _closeData[_currentPos] = close;
                _volData[_currentPos] = volume;

                // Сбрасываем дополнительные данные для этой позиции
                ResetAdditionalData(_currentPos);

                _currentPos++;
                _dataCount = Math.Min(_dataCount + 1, BUFFER_SIZE);

                if (_currentPos >= BUFFER_SIZE)
                {
                    _bufferFilled = true;
                    _currentPos = BUFFER_SIZE;
                    DataShifted?.Invoke();
                }

                DataUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Добавление сигнала (стрелки) покупки
        /// </summary>
        public void PushBuySignal(double price, double offsetPercent = 0.03)
        {
            lock (_lock)
            {
                if (_dataCount == 0) return;

                int targetIndex = _currentPos > 0 ? _currentPos - 1 : BUFFER_SIZE - 1;
                _buySignals[targetIndex] = price * (1 - offsetPercent);
                _sellSignals[targetIndex] = Chart.NoValue; // Сбрасываем противоположный сигнал

                DataUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Добавление сигнала (стрелки) продажи
        /// </summary>
        public void PushSellSignal(double price, double offsetPercent = 0.03)
        {
            lock (_lock)
            {
                if (_dataCount == 0) return;

                int targetIndex = _currentPos > 0 ? _currentPos - 1 : BUFFER_SIZE - 1;
                _sellSignals[targetIndex] = price * (1 + offsetPercent);
                _buySignals[targetIndex] = Chart.NoValue; // Сбрасываем противоположный сигнал

                DataUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Добавление произвольных данных (для будущего расширения)
        /// </summary>
        public void PushCustomData<T>(T data, string dataType)
        {
            lock (_lock)
            {
                if (_dataCount == 0) return;

                int targetIndex = _currentPos > 0 ? _currentPos - 1 : BUFFER_SIZE - 1;

                // Здесь можно добавлять разные типы данных
                switch (dataType)
                {
                    case "Marker":
                        if (data is double markerValue)
                            _markers[targetIndex] = markerValue;
                        break;
                    case "Indicator":
                        if (data is double indicatorValue)
                            _customIndicators[targetIndex] = indicatorValue;
                        break;
                        // Можно добавить больше типов
                }

                DataUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Получение всех данных для рендеринга (используется при отображении)
        /// </summary>
        public ChartDataSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new ChartDataSnapshot
                {
                    TimeStamps = GetValidSlice(_timeStamps),
                    HighData = GetValidSlice(_highData),
                    LowData = GetValidSlice(_lowData),
                    OpenData = GetValidSlice(_openData),
                    CloseData = GetValidSlice(_closeData),
                    VolData = GetValidSlice(_volData),
                    BuySignals = GetValidSlice(_buySignals),
                    SellSignals = GetValidSlice(_sellSignals),
                    Markers = GetValidSlice(_markers),
                    CustomIndicators = GetValidSlice(_customIndicators),
                    Count = _dataCount,
                    IsBufferFilled = _bufferFilled
                };
            }
        }

        private double[] GetValidSlice(double[] source)
        {
            if (_dataCount == 0) return Array.Empty<double>();

            var slice = new double[_dataCount];
            int startIndex = _bufferFilled ? (_currentPos % BUFFER_SIZE) : 0;

            for (int i = 0; i < _dataCount; i++)
            {
                int sourceIndex = (startIndex + i) % BUFFER_SIZE;
                slice[i] = source[sourceIndex];
            }

            return slice;
        }

        private void ShiftAllArrays()
        {
            for (int i = 1; i < BUFFER_SIZE; i++)
            {
                _timeStamps[i - 1] = _timeStamps[i];
                _highData[i - 1] = _highData[i];
                _lowData[i - 1] = _lowData[i];
                _openData[i - 1] = _openData[i];
                _closeData[i - 1] = _closeData[i];
                _volData[i - 1] = _volData[i];
                _buySignals[i - 1] = _buySignals[i];
                _sellSignals[i - 1] = _sellSignals[i];
                _markers[i - 1] = _markers[i];
                _customIndicators[i - 1] = _customIndicators[i];
            }

            // Очищаем последнюю позицию
            ResetAdditionalData(BUFFER_SIZE - 1);
        }

        private void ResetAdditionalData(int index)
        {
            _buySignals[index] = Chart.NoValue;
            _sellSignals[index] = Chart.NoValue;
            _markers[index] = Chart.NoValue;
            _customIndicators[index] = Chart.NoValue;
        }

        public string Symbol => _symbol;
        public int DataCount => _dataCount;
        public bool IsBufferFilled => _bufferFilled;
    }

    /// <summary>
    /// Снимок данных для рендеринга
    /// </summary>
    public class ChartDataSnapshot
    {
        public double[] TimeStamps { get; set; } = Array.Empty<double>();
        public double[] HighData { get; set; } = Array.Empty<double>();
        public double[] LowData { get; set; } = Array.Empty<double>();
        public double[] OpenData { get; set; } = Array.Empty<double>();
        public double[] CloseData { get; set; } = Array.Empty<double>();
        public double[] VolData { get; set; } = Array.Empty<double>();
        public double[] BuySignals { get; set; } = Array.Empty<double>();
        public double[] SellSignals { get; set; } = Array.Empty<double>();
        public double[] Markers { get; set; } = Array.Empty<double>();
        public double[] CustomIndicators { get; set; } = Array.Empty<double>();
        public int Count { get; set; }
        public bool IsBufferFilled { get; set; }
    }
}
