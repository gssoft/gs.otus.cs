// GS.Trade.Strategies
// DiscreteRandomTradeStrategy.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using GS.Trade.Core;
using GS.Trade.Abstractions;

namespace GS.Trade.Strategies
{
    public class DiscreteRandomTradeStrategy : TradeStrategy, IRandomTradeStrategy
    {
        private readonly Random _random;
        private int _ticksUntilNextDecision;
        private const int DECISION_INTERVAL_TICKS = 10;
        private const int MAX_POSITION = 10;
        private const int MIN_POSITION = -10;
        private static long _tradeCounter = 1;
        private readonly object _lockObject = new object();
        private bool _isRunning;
        private int _currentDecision;

        public DiscreteRandomTradeStrategy(string ticker)
            : base(ticker, "DiscreteRandomTradeStrategy")
        {
            _random = new Random();
            _ticksUntilNextDecision = 0; // Начинаем с немедленного принятия решения
            _currentDecision = 0; // Начальное состояние - Flat
        }

        public void StartTrading(CancellationToken cancellationToken = default)
        {
            _isRunning = true;
            Task.Run(async () =>
            {
                var priceGenerator = new Random(DateTime.Now.Millisecond);
                decimal basePrice = 100.0m;

                while (_isRunning && !cancellationToken.IsCancellationRequested)
                {
                    // Генерируем случайное изменение цены (-2% до +2%)
                    decimal changePercent = (decimal)(_random.NextDouble() * 0.04 - 0.02);
                    decimal currentPrice = basePrice * (1 + changePercent);
                    basePrice = currentPrice;

                    // Обновляем цену в стратегии
                    UpdateMarketPrice(currentPrice);

                    // Обрабатываем торговое решение
                    ProcessTradingDecision(currentPrice);

                    // Выводим текущее состояние
                    PrintCurrentState(currentPrice);

                    await Task.Delay(1000, cancellationToken);
                }
            }, cancellationToken);
        }

        public void StopTrading()
        {
            _isRunning = false;
        }

        private void ProcessTradingDecision(decimal currentPrice)
        {
            lock (_lockObject)
            {
                _ticksUntilNextDecision--;

                // Если пришло время принимать новое решение
                if (_ticksUntilNextDecision <= 0)
                {
                    // Принимаем новое решение: -1 (Short), 0 (Flat), +1 (Long)
                    _currentDecision = _random.Next(-1, 2);
                    _ticksUntilNextDecision = DECISION_INTERVAL_TICKS;

                    Console.WriteLine($"=== ПРИНЯТО РЕШЕНИЕ: {GetDecisionName(_currentDecision)} ===");
                }

                // Исполняем текущее решение (если оно не Flat)
                if (_currentDecision != 0)
                {
                    ExecuteTrade(currentPrice, _currentDecision);
                }
                // При решении Flat позиция автоматически сохраняется
            }
        }

        private void ExecuteTrade(decimal currentPrice, int direction)
        {
            int currentPosition = Position.NetQuantity;
            int targetPosition = direction > 0 ? 1 : -1;

            // Если уже достигнута желаемая позиция, не делаем ничего
            if ((direction > 0 && currentPosition >= targetPosition) ||
                (direction < 0 && currentPosition <= targetPosition))
            {
                return;
            }

            // Проверяем границы позиции
            int newPosition = currentPosition + (direction > 0 ? 1 : -1);
            if (newPosition > MAX_POSITION || newPosition < MIN_POSITION)
            {
                Console.WriteLine($"    ⚠️ ДОСТИГНУТ ЛИМИТ ПОЗИЦИИ: {newPosition}");
                return;
            }

            var tradeSide = direction > 0 ? TradeSide.Buy : TradeSide.Sell;
            int quantity = 1;

            var trade = new Core.Trade
            {
                Ticker = Ticker,
                Price = currentPrice,
                Qty = quantity,
                Side = tradeSide,
                DateTime = DateTime.Now,
                TradeNumber = Interlocked.Increment(ref _tradeCounter)
            };

            ProcessTrade(trade);
        }

        private string GetDecisionName(int decision)
        {
            return decision switch
            {
                1 => "LONG",
                -1 => "SHORT",
                0 => "FLAT (удержание)",
                _ => "НЕИЗВЕСТНО"
            };
        }

        private string GetPositionStatusEmoji()
        {
            return Position.NetQuantity switch
            {
                > 0 => "📈",
                < 0 => "📉",
                _ => "➡️"
            };
        }

        private void PrintCurrentState(decimal currentPrice)
        {
            var position = Position;
            var summary = GetStrategySummary();

            string decisionInfo = _ticksUntilNextDecision > 0
                ? $"Текущее решение: {GetDecisionName(_currentDecision)} | Тиков до след. решения: {_ticksUntilNextDecision}"
                : "⚠️ ПРИНИМАЕТСЯ НОВОЕ РЕШЕНИЕ...";

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} | " +
                            $"{Ticker} {GetPositionStatusEmoji()} | " +
                            $"Цена: {currentPrice:F2} | " +
                            $"Позиция: {position.NetQuantity} | " +
                            $"{decisionInfo} | " +
                            $"Unrealized PnL: {summary.UnrealizedProfit:F2}");
        }

        protected override void OnDealExecuted(IDeal deal)
        {
            base.OnDealExecuted(deal);
            Console.WriteLine($"    >>> СДЕЛКА ИСПОЛНЕНА: {deal}");
        }
    }
}