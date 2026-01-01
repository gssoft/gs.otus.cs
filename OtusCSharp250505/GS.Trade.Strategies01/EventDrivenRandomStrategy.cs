// GS.Trade.Strategies
// EventDrivenRandomStrategy.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using GS.Trade.Core;
using GS.Trade.Abstractions;

namespace GS.Trade.Strategies
{
    public class EventDrivenRandomStrategy : TradeStrategy, IRandomTradeStrategy
    {
        private readonly Random _random;
        private int _ticksRemaining;
        private const int CYCLE_TICKS = 10;
        private readonly object _lockObject = new object();
        private bool _isRunning;
        private TradingAction _currentAction;
        private bool _actionExecutedInCycle;

        // События для подписки
        public event Action<ITrade>? OnTradeExecuted;
        public event Action<IDeal>? OnDealClosed;
        public event Action<IPositionSummary>? OnPositionChanged;
        public event Action<IOrder>? OnOrderCreated;

        public EventDrivenRandomStrategy(string ticker)
            : base(ticker, "EventDrivenRandomStrategy")
        {
            _random = new Random();
            _ticksRemaining = 0;
            _currentAction = TradingAction.Flat;
            _actionExecutedInCycle = false;

            // Подписываемся на событие закрытия сделки из позиции
            // !!!!!!!!!!!! Возможны варианты
            // _position.DealClosed += deal => OnDealClosed?.Invoke(deal);
        }

        public void StartTrading(CancellationToken cancellationToken = default)
        {
            _isRunning = true;
            Console.WriteLine($"🚀 СТРАТЕГИЯ ЗАПУЩЕНА: {StrategyName} для {Ticker}");
        }

        public void StopTrading()
        {
            _isRunning = false;
            Console.WriteLine($"🛑 СТРАТЕГИЯ ОСТАНОВЛЕНА: {StrategyName} для {Ticker}");
        }

        // Обработка внешних тиков
        public override void ProcessTick(ITick tick)
        {
            if (!_isRunning) return;

            base.ProcessTick(tick); // Обновляем цену
            ProcessTradingCycle(tick.Price);

            // Вызываем событие изменения позиции
            OnPositionChanged?.Invoke(GetTradePosition());
        }

        private void ProcessTradingCycle(decimal currentPrice)
        {
            lock (_lockObject)
            {
                _ticksRemaining--;

                if (_ticksRemaining <= 0)
                {
                    _ticksRemaining = CYCLE_TICKS;
                    _actionExecutedInCycle = false;
                    _currentAction = GetRandomAction();

                    Console.WriteLine($"\n=== НОВЫЙ ЦИКЛ: {GetActionDescription(_currentAction)} ({CYCLE_TICKS} тиков) ===");
                }

                if (!_actionExecutedInCycle && _ticksRemaining == CYCLE_TICKS)
                {
                    ExecuteSingleAction(currentPrice);
                    _actionExecutedInCycle = true;
                }
            }

            PrintCurrentState(currentPrice);
        }

        private TradingAction GetRandomAction()
        {
            var values = Enum.GetValues(typeof(TradingAction));
            return (TradingAction)values.GetValue(_random.Next(values.Length))!;
        }

        private void ExecuteSingleAction(decimal currentPrice)
        {
            switch (_currentAction)
            {
                case TradingAction.Buy:
                    ExecuteBuy(currentPrice);
                    break;
                case TradingAction.Sell:
                    ExecuteSell(currentPrice);
                    break;
                case TradingAction.Flat:
                //    Console.WriteLine("    💤 УДЕРЖАНИЕ ПОЗИЦИИ");
                    break;
            }
        }

        private void ExecuteBuy(decimal currentPrice)
        {
            if (Position.NetQuantity >= 10)
            {
                Console.WriteLine("    ⚠️ ДОСТИГНУТ ЛИМИТ LONG ПОЗИЦИИ");
                return;
            }

            // Создаем ордер
            var order = CreateOrder(currentPrice, 1, TradeSide.Buy);
            OnOrderCreated?.Invoke(order);

            // Исполняем ордер
            var trade = ExecuteOrder(order, currentPrice);
            OnTradeExecuted?.Invoke(trade);

            Console.WriteLine("    🟢 ВЫПОЛНЕНА ПОКУПКА 1 ЛОТА");
        }

        private void ExecuteSell(decimal currentPrice)
        {
            if (Position.NetQuantity <= -10)
            {
                Console.WriteLine("    ⚠️ ДОСТИГНУТ ЛИМИТ SHORT ПОЗИЦИИ");
                return;
            }

            // Создаем ордер
            var order = CreateOrder(currentPrice, 1, TradeSide.Sell);
            OnOrderCreated?.Invoke(order);

            // Исполняем ордер
            var trade = ExecuteOrder(order, currentPrice);
            OnTradeExecuted?.Invoke(trade);

            Console.WriteLine("    🔴 ВЫПОЛНЕНА ПРОДАЖА 1 ЛОТА");
        }

        protected override void OnDealExecuted(IDeal deal)
        {
            base.OnDealExecuted(deal);
            OnDealClosed?.Invoke(deal);
        }

        private string GetPositionEmoji()
        {
            return Position.NetQuantity switch
            {
                > 0 => "📈",
                < 0 => "📉",
                _ => "➡️"
            };
        }

        private string GetActionEmoji(TradingAction action)
        {
            return action switch
            {
                TradingAction.Buy => "🟢",
                TradingAction.Sell => "🔴",
                TradingAction.Flat => "⚪",
                _ => "❓"
            };
        }

        private string GetActionDescription(TradingAction action)
        {
            return action switch
            {
                TradingAction.Buy => "ПОКУПКА",
                TradingAction.Sell => "ПРОДАЖА",
                TradingAction.Flat => "УДЕРЖАНИЕ",
                _ => "НЕИЗВЕСТНО"
            };
        }

        private string GetCycleProgress()
        {
            if (_ticksRemaining == CYCLE_TICKS)
                return "🚀 Начало";
            else if (_ticksRemaining <= 3)
                return $"⏳ Завершение ({_ticksRemaining})";
            else
                return $"⏳ В процессе ({_ticksRemaining})";
        }

        private void PrintCurrentState(decimal currentPrice)
        {
            var position = Position;
            var summary = GetStrategySummary();

            string actionInfo = $"{GetActionEmoji(_currentAction)} {GetActionDescription(_currentAction)}";
            string positionInfo = $"Поз: {position.NetQuantity} {GetPositionEmoji()}";
            string cycleInfo = GetCycleProgress();
            string executedInfo = _actionExecutedInCycle ? "✓" : "✗";

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} | " +
                            $"{Ticker} | " +
                            $"Цена: {currentPrice:F2} | " +
                            $"{positionInfo} | " +
                            $"{actionInfo} {executedInfo} | " +
                            $"{cycleInfo} | " +
                            $"P&L: {summary.UnrealizedProfit:F2}");
        }
    }

    public class EventDrivenRandomStrategy01 : TradeStrategy, IRandomTradeStrategy
    {
        private readonly Random _random;
        private int _ticksRemaining;
        // private const int CYCLE_TICKS = 10;
        private readonly int _cycleTicks;
        private readonly object _lockObject = new object();
        private bool _isRunning;
        private TradingAction _currentAction;
        private bool _actionExecutedInCycle;

        // События для подписки
        public event Action<ITrade>? OnTradeExecuted;
        public event Action<IDeal>? OnDealClosed;
        public event Action<IPositionSummary>? OnPositionChanged;
        public event Action<IOrder>? OnOrderCreated;

        // Обновленный конструктор
        public EventDrivenRandomStrategy01(string ticker, int cycleTicks = 10)
            : base(ticker, $"EventDrivenRandomStrategy_{cycleTicks}")
        {
            _random = new Random();
            _ticksRemaining = 0;
            _cycleTicks = cycleTicks; // Сохраняем параметр
            _currentAction = TradingAction.Flat;
            _actionExecutedInCycle = false;
        }

        // Новый конструктор с тремя параметрами (ticker + cycleTicks + strategyName)
        public EventDrivenRandomStrategy01(string ticker, int cycleTicks, string strategyName)
            : base(ticker, strategyName)
        {
            _random = new Random();
            _ticksRemaining = 0;
            _cycleTicks = cycleTicks;
            _currentAction = TradingAction.Flat;
            _actionExecutedInCycle = false;
        }

        public void StartTrading(CancellationToken cancellationToken = default)
        {
            _isRunning = true;
            Console.WriteLine($"🚀 СТРАТЕГИЯ ЗАПУЩЕНА: {StrategyName} для {Ticker}");
        }

        public void StopTrading()
        {
            _isRunning = false;
            Console.WriteLine($"🛑 СТРАТЕГИЯ ОСТАНОВЛЕНА: {StrategyName} для {Ticker}");
        }

        // Обработка внешних тиков
        public override void ProcessTick(ITick tick)
        {
            if (!_isRunning) return;

            base.ProcessTick(tick); // Обновляем цену
            ProcessTradingCycle(tick.Price);

            // Вызываем событие изменения позиции
            OnPositionChanged?.Invoke(GetTradePosition());
        }

        // В методе ProcessTradingCycle замените CYCLE_TICKS на _cycleTicks
        private void ProcessTradingCycle(decimal currentPrice)
        {
            lock (_lockObject)
            {
                _ticksRemaining--;

                if (_ticksRemaining <= 0)
                {
                    _ticksRemaining = _cycleTicks; // Здесь используем поле
                    _actionExecutedInCycle = false;
                    _currentAction = GetRandomAction();

                    Console.WriteLine($"\n=== НОВЫЙ ЦИКЛ: {GetActionDescription(_currentAction)} ({_cycleTicks} тиков) ===");
                }

                if (!_actionExecutedInCycle && _ticksRemaining == _cycleTicks)
                {
                    ExecuteSingleAction(currentPrice);
                    _actionExecutedInCycle = true;
                }
            }

            PrintCurrentState(currentPrice);
        }

        private TradingAction GetRandomAction()
        {
            var values = Enum.GetValues(typeof(TradingAction));
            return (TradingAction)values.GetValue(_random.Next(values.Length))!;
        }

        private void ExecuteSingleAction(decimal currentPrice)
        {
            switch (_currentAction)
            {
                case TradingAction.Buy:
                    ExecuteBuy(currentPrice);
                    break;
                case TradingAction.Sell:
                    ExecuteSell(currentPrice);
                    break;
                case TradingAction.Flat:
                    //    Console.WriteLine("    💤 УДЕРЖАНИЕ ПОЗИЦИИ");
                    break;
            }
        }

        private void ExecuteBuy(decimal currentPrice)
        {
            if (Position.NetQuantity >= 10)
            {
                Console.WriteLine("    ⚠️ ДОСТИГНУТ ЛИМИТ LONG ПОЗИЦИИ");
                return;
            }

            // Создаем ордер
            var order = CreateOrder(currentPrice, 1, TradeSide.Buy);
            OnOrderCreated?.Invoke(order);

            // Исполняем ордер
            var trade = ExecuteOrder(order, currentPrice);
            OnTradeExecuted?.Invoke(trade);

            Console.WriteLine("    🟢 ВЫПОЛНЕНА ПОКУПКА 1 ЛОТА");
        }

        private void ExecuteSell(decimal currentPrice)
        {
            if (Position.NetQuantity <= -10)
            {
                Console.WriteLine("    ⚠️ ДОСТИГНУТ ЛИМИТ SHORT ПОЗИЦИИ");
                return;
            }

            // Создаем ордер
            var order = CreateOrder(currentPrice, 1, TradeSide.Sell);
            OnOrderCreated?.Invoke(order);

            // Исполняем ордер
            var trade = ExecuteOrder(order, currentPrice);
            OnTradeExecuted?.Invoke(trade);

            Console.WriteLine("    🔴 ВЫПОЛНЕНА ПРОДАЖА 1 ЛОТА");
        }

        protected override void OnDealExecuted(IDeal deal)
        {
            base.OnDealExecuted(deal);
            OnDealClosed?.Invoke(deal);
        }

        private string GetPositionEmoji()
        {
            return Position.NetQuantity switch
            {
                > 0 => "📈",
                < 0 => "📉",
                _ => "➡️"
            };
        }

        private string GetActionEmoji(TradingAction action)
        {
            return action switch
            {
                TradingAction.Buy => "🟢",
                TradingAction.Sell => "🔴",
                TradingAction.Flat => "⚪",
                _ => "❓"
            };
        }

        private string GetActionDescription(TradingAction action)
        {
            return action switch
            {
                TradingAction.Buy => "ПОКУПКА",
                TradingAction.Sell => "ПРОДАЖА",
                TradingAction.Flat => "УДЕРЖАНИЕ",
                _ => "НЕИЗВЕСТНО"
            };
        }

        // В методе GetCycleProgress тоже замените CYCLE_TICKS
        private string GetCycleProgress()
        {
            if (_ticksRemaining == _cycleTicks)
                return "🚀 Начало";
            else if (_ticksRemaining <= 3)
                return $"⏳ Завершение ({_ticksRemaining})";
            else
                return $"⏳ В процессе ({_ticksRemaining})";
        }

        //private string GetCycleProgress()
        //{
        //    if (_ticksRemaining == CYCLE_TICKS)
        //        return "🚀 Начало";
        //    else if (_ticksRemaining <= 3)
        //        return $"⏳ Завершение ({_ticksRemaining})";
        //    else
        //        return $"⏳ В процессе ({_ticksRemaining})";
        //}

        private void PrintCurrentState(decimal currentPrice)
        {
            var position = Position;
            var summary = GetStrategySummary();

            string actionInfo = $"{GetActionEmoji(_currentAction)} {GetActionDescription(_currentAction)}";
            string positionInfo = $"Поз: {position.NetQuantity} {GetPositionEmoji()}";
            string cycleInfo = GetCycleProgress();
            string executedInfo = _actionExecutedInCycle ? "✓" : "✗";

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} | " +
                            $"{Ticker} | " +
                            $"Цена: {currentPrice:F2} | " +
                            $"{positionInfo} | " +
                            $"{actionInfo} {executedInfo} | " +
                            $"{cycleInfo} | " +
                            $"P&L: {summary.UnrealizedProfit:F2}");
        }
    }

    public enum TradingAction
    {
        Buy,   // Купить 1 лот
        Sell,  // Продать 1 лот  
        Flat   // Сохранить текущую позицию (ничего не делать)
    }
}