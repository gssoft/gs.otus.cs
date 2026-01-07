// Services/TradingFactory.cs

using GS.Trade.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;

namespace TradingPlatform.Services
{
    public class TradingFactory : ITradingFactory
    {
        private long _tradeCounter = 0;
        private long _tickCounter = 0;
        private long _orderCounter = 0;

        public ITrade CreateTrade(string ticker, decimal price, int qty, TradeSide side, DateTime dateTime, long tradeNumber)
        {
            return new SimpleTrade
            {
                Ticker = ticker,
                Price = price,
                Qty = qty,
                Side = side,
                DateTime = dateTime,
                TradeNumber = tradeNumber
            };
        }

        public IDeal CreateDeal(ITrade openTrade, ITrade closeTrade, int qty, DateTime dealTime)
        {
            return new SimpleDeal
            {
                Number = Interlocked.Increment(ref _tradeCounter),
                DateTime = dealTime,
                Ticker = openTrade.Ticker,
                OpenTradeNumber = openTrade.TradeNumber,
                CloseTradeNumber = closeTrade.TradeNumber,
                Qty = qty,
                Side = openTrade.Side,
                OpenPrice = openTrade.Price,
                ClosePrice = closeTrade.Price,
                PnL = (closeTrade.Price - openTrade.Price) * qty * (openTrade.Side == TradeSide.Buy ? 1 : -1)
            };
        }

        public ITick CreateTick(string ticker, decimal price, decimal volume, DateTime dateTime, long tickNumber)
        {
            return new SimpleTick
            {
                Ticker = ticker,
                Price = price,
                Volume = volume,
                DateTime = dateTime,
                TickNumber = tickNumber
            };
        }

        public ICandleStick CreateCandle(string ticker, DateTime openTime, DateTime closeTime, decimal open,
            decimal high, decimal low, decimal close, decimal volume, TimeSpan timeFrame)
        {
            return new SimpleCandle
            {
                Ticker = ticker,
                OpenTime = openTime,
                CloseTime = closeTime,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                TimeFrame = timeFrame
            };
        }

        public IOrder CreateOrder(string ticker, decimal price, int qty, TradeSide side, DateTime dateTime,
            long orderNumber, string strategyName)
        {
            return new SimpleOrder
            {
                Ticker = ticker,
                Price = price,
                Qty = qty,
                Side = side,
                DateTime = dateTime,
                OrderNumber = orderNumber,
                Status = OrderStatus.Pending,
                StrategyName = strategyName
            };
        }
    }

    // Простые реализации интерфейсов
    public class SimpleTrade : ITrade
    {
        public string Ticker { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Qty { get; set; }
        public TradeSide Side { get; set; }
        public DateTime DateTime { get; set; }
        public long TradeNumber { get; set; }
    }

    public class SimpleDeal : IDeal
    {
        public long Number { get; set; }
        public DateTime DateTime { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public long OpenTradeNumber { get; set; }
        public long CloseTradeNumber { get; set; }
        public int Qty { get; set; }
        public TradeSide Side { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal PnL { get; set; }
    }

    public class SimpleOrder : IOrder
    {
        public string Ticker { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Qty { get; set; }
        public TradeSide Side { get; set; }
        public DateTime DateTime { get; set; }
        public long OrderNumber { get; set; }
        public OrderStatus Status { get; set; }
        public string? StrategyName { get; set; }
        public DateTime? ExecutionTime { get; set; }
        public decimal? ExecutionPrice { get; set; }
    }

    public class SimpleCandle : ICandleStick
    {
        public string Ticker { get; set; } = string.Empty;
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public TimeSpan TimeFrame { get; set; }
    }

    public class SimpleTick : ITick
    {
        public string Ticker { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
        public DateTime DateTime { get; set; }
        public long TickNumber { get; set; }
    }
}
