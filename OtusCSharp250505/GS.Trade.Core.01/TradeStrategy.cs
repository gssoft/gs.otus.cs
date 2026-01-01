// GS.Trade.Core
// TradeStrategy.cs

// GS.Trade.Core
// Обновляем TradeStrategy.cs

using System;
using System.Collections.Generic;
using System.Linq;
using GS.Trade.Abstractions;

namespace GS.Trade.Core
{
    public abstract class TradeStrategy : ITradeStrategy
    {
        private readonly List<IDeal> _closedDeals = new();
        private readonly List<ITrade> _allTrades = new();
        private readonly List<IOrder> _orders = new();
        private long _dealCounter = 1;
        private long _orderCounter = 1;
        protected readonly Position _position;

        public string Ticker { get; }
        public string StrategyName { get; protected set; }
        public decimal RealizedProfit { get; private set; }
        public IReadOnlyList<IDeal> ClosedDeals => _closedDeals.AsReadOnly();
        public IReadOnlyList<ITrade> AllTrades => _allTrades.AsReadOnly();
        public IReadOnlyList<IOrder> Orders => _orders.AsReadOnly();
        public IPosition Position => _position;

        protected TradeStrategy(string ticker, string strategyName, ITradingFactory? factory = null)
        {
            Ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
            StrategyName = strategyName ?? throw new ArgumentNullException(nameof(strategyName));
            _position = new Position(this, factory);
        }

        public virtual void ProcessTrade(ITrade trade)
        {
            if (trade.Ticker != Ticker)
            {
                throw new ArgumentException($"Trade ticker {trade.Ticker} does not match strategy ticker {Ticker}");
            }

            _allTrades.Add(trade);
            _position.ProcessTrade(trade);
        }

        // Новая функция для обработки тиков
        public virtual void ProcessTick(ITick tick)
        {
            if (tick.Ticker != Ticker)
            {
                throw new ArgumentException($"Tick ticker {tick.Ticker} does not match strategy ticker {Ticker}");
            }

            UpdateMarketPrice(tick.Price);
            OnTickProcessed(tick);
        }

        // Новая функция для обработки свечей
        public virtual void ProcessCandle(ICandleStick candle)
        {
            if (candle.Ticker != Ticker)
            {
                throw new ArgumentException($"Candle ticker {candle.Ticker} does not match strategy ticker {Ticker}");
            }

            UpdateMarketPrice(candle.Close);
            OnCandleProcessed(candle);
        }

        public virtual void UpdateMarketPrice(decimal price)
        {
            _position.LastPrice = price;
        }

        public void OnDealClosed(IDeal deal)
        {
            deal.Number = _dealCounter++;
            _closedDeals.Add(deal);
            RealizedProfit += deal.PnL;

            OnDealExecuted(deal);
        }

        // Метод для создания ордера
        protected IOrder CreateOrder(decimal price, int qty, TradeSide side)
        {
            var order = new Order
            {
                Ticker = Ticker,
                Price = price,
                Qty = qty,
                Side = side,
                DateTime = DateTime.Now,
                OrderNumber = _orderCounter++,
                StrategyName = StrategyName,
                Status = OrderStatus.Pending
            };

            _orders.Add(order);
            OnOrderCreated(order);
            return order;
        }

        // Метод для исполнения ордера
        protected ITrade ExecuteOrder(IOrder order, decimal executionPrice)
        {
            if (order.Status != OrderStatus.Pending)
                throw new InvalidOperationException("Order is not in pending status");

            var orderIndex = _orders.FindIndex(o => o.OrderNumber == order.OrderNumber);
            if (orderIndex >= 0)
            {
                var executedOrder = new Order
                {
                    Ticker = order.Ticker,
                    Price = order.Price,
                    Qty = order.Qty,
                    Side = order.Side,
                    DateTime = order.DateTime,
                    OrderNumber = order.OrderNumber,
                    StrategyName = order.StrategyName,
                    Status = OrderStatus.Filled,
                    ExecutionTime = DateTime.Now,
                    ExecutionPrice = executionPrice
                };
                _orders[orderIndex] = executedOrder;
            }

            var trade = new Trade
            {
                Ticker = order.Ticker,
                Price = executionPrice,
                Qty = order.Qty,
                Side = order.Side,
                DateTime = DateTime.Now,
                TradeNumber = GenerateTradeNumber()
            };

            ProcessTrade(trade);
            OnOrderExecuted(order, trade);
            return trade;
        }

        protected virtual void OnDealExecuted(IDeal deal)
        {
            // Переопределить в производных классах для специфической логики
        }

        protected virtual void OnTickProcessed(ITick tick)
        {
            // Переопределить в производных классах для реакции на тики
        }

        protected virtual void OnCandleProcessed(ICandleStick candle)
        {
            // Переопределить в производных классах для реакции на свечи
        }

        protected virtual void OnOrderCreated(IOrder order)
        {
            // Переопределить в производных классах для логики создания ордеров
        }

        protected virtual void OnOrderExecuted(IOrder order, ITrade trade)
        {
            // Переопределить в производных классах для логики исполнения ордеров
        }

        public virtual void CloseAllPositions()
        {
            _position.ClosePosition();
        }

        public IStrategySummary GetStrategySummary()
        {
            var posSummary = _position.GetSummary();

            return new StrategySummary
            {
                Ticker = Ticker,
                StrategyName = StrategyName,
                Status = posSummary.Status,
                NetQuantity = posSummary.NetQuantity,
                CurrentPrice = posSummary.CurrentPrice,
                RealizedProfit = RealizedProfit,
                UnrealizedProfit = posSummary.UnrealizedProfit,
                TotalProfit = RealizedProfit + posSummary.UnrealizedProfit,
                OpenTradesCount = posSummary.OpenTradesCount,
                ClosedDealsCount = _closedDeals.Count
            };
        }

        // Новые методы для экспорта данных
        public IReadOnlyList<ITrade> GetTrades() => _allTrades.AsReadOnly();
        public IReadOnlyList<IDeal> GetDeals() => _closedDeals.AsReadOnly();
        public IPositionSummary GetTradePosition() => _position.GetSummary();
        public decimal GetRealizedProfit() => RealizedProfit;
        public decimal GetUnrealizedProfit() => _position.UnrealizedProfit;

        private static long _globalTradeCounter = 1;
        private static long GenerateTradeNumber() => Interlocked.Increment(ref _globalTradeCounter);

        public override string ToString()
        {
            var summary = GetStrategySummary();
            return $"Strategy: {summary.StrategyName} | " +
                   $"Ticker: {summary.Ticker} | " +
                   $"Status: {summary.Status} | " +
                   $"NetQty: {summary.NetQuantity} | " +
                   $"RealizedP&L: {summary.RealizedProfit:F2} | " +
                   $"UnrealizedP&L: {summary.UnrealizedProfit:F2} | " +
                   $"TotalP&L: {summary.TotalProfit:F2} | " +
                   $"Trades: {_allTrades.Count} | " +
                   $"Deals: {_closedDeals.Count} | " +
                   $"Orders: {_orders.Count}";
        }
    }
}

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using GS.Trade.Abstractions;

//namespace GS.Trade.Core
//{
//    public abstract class TradeStrategy : ITradeStrategy
//    {
//        private readonly List<IDeal> _closedDeals = new();
//        private long _dealCounter = 1;
//        protected readonly Position _position;

//        public string Ticker { get; }
//        public string StrategyName { get; protected set; }
//        public decimal RealizedProfit { get; private set; }
//        public IReadOnlyList<IDeal> ClosedDeals => _closedDeals.AsReadOnly();
//        public IPosition Position => _position;

//        protected TradeStrategy(string ticker, string strategyName, ITradingFactory? factory = null)
//        {
//            Ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
//            StrategyName = strategyName ?? throw new ArgumentNullException(nameof(strategyName));
//            _position = new Position(this, factory);
//        }

//        public virtual void ProcessTrade(ITrade trade)
//        {
//            if (trade.Ticker != Ticker)
//            {
//                throw new ArgumentException($"Trade ticker {trade.Ticker} does not match strategy ticker {Ticker}");
//            }

//            _position.ProcessTrade(trade);
//        }

//        public virtual void UpdateMarketPrice(decimal price)
//        {
//            _position.LastPrice = price;
//        }

//        public void OnDealClosed(IDeal deal)
//        {
//            deal.Number = _dealCounter++;
//            _closedDeals.Add(deal);
//            RealizedProfit += deal.PnL;

//            OnDealExecuted(deal);
//        }

//        protected virtual void OnDealExecuted(IDeal deal)
//        {
//            // Переопределить в производных классах для специфической логики
//        }

//        public virtual void CloseAllPositions()
//        {
//            _position.ClosePosition();
//        }

//        public IStrategySummary GetStrategySummary()
//        {
//            var posSummary = _position.GetSummary();

//            return new StrategySummary
//            {
//                Ticker = Ticker,
//                StrategyName = StrategyName,
//                Status = posSummary.Status,
//                NetQuantity = posSummary.NetQuantity,
//                CurrentPrice = posSummary.CurrentPrice,
//                RealizedProfit = RealizedProfit,
//                UnrealizedProfit = posSummary.UnrealizedProfit,
//                TotalProfit = RealizedProfit + posSummary.UnrealizedProfit,
//                OpenTradesCount = posSummary.OpenTradesCount,
//                ClosedDealsCount = _closedDeals.Count
//            };
//        }

//        public override string ToString()
//        {
//            var summary = GetStrategySummary();
//            return $"Strategy: {summary.StrategyName} | " +
//                   $"Ticker: {summary.Ticker} | " +
//                   $"Status: {summary.Status} | " +
//                   $"NetQty: {summary.NetQuantity} | " +
//                   $"RealizedP&L: {summary.RealizedProfit:F2} | " +
//                   $"UnrealizedP&L: {summary.UnrealizedProfit:F2} | " +
//                   $"TotalP&L: {summary.TotalProfit:F2}";
//        }
//    }
//}