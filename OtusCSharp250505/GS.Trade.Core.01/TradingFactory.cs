// GS.Trade.Core
// TradingFactory.cs

// GS.Trade.Core
// Обновляем TradingFactory.cs

using GS.Trade.Abstractions;
using GS.Trade.Core;

public class TradingFactory : ITradingFactory
{
    public ITrade CreateTrade(string ticker, decimal price, int qty, TradeSide side, DateTime dateTime, long tradeNumber)
    {
        return new Trade
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
        if (openTrade.Ticker != closeTrade.Ticker)
            throw new ArgumentException("Trade tickers must match");

        decimal pnl = CalculatePnL(openTrade, closeTrade, qty);

        return new Deal
        {
            DateTime = dealTime,
            Ticker = openTrade.Ticker,
            OpenTradeNumber = openTrade.TradeNumber,
            CloseTradeNumber = closeTrade.TradeNumber,
            Qty = qty,
            Side = openTrade.Side,
            OpenPrice = openTrade.Price,
            ClosePrice = closeTrade.Price,
            PnL = pnl
        };
    }

    public ITick CreateTick(string ticker, decimal price, decimal volume, DateTime dateTime, long tickNumber)
    {
        return new Tick
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
        return new CandleStick
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
        return new Order
        {
            Ticker = ticker,
            Price = price,
            Qty = qty,
            Side = side,
            DateTime = dateTime,
            OrderNumber = orderNumber,
            StrategyName = strategyName,
            Status = OrderStatus.Pending
        };
    }

    private static decimal CalculatePnL(ITrade openTrade, ITrade closeTrade, int qty)
    {
        return openTrade.Side == TradeSide.Buy
            ? (closeTrade.Price - openTrade.Price) * qty
            : (openTrade.Price - closeTrade.Price) * qty;
    }
}

//using System;
//using GS.Trade.Abstractions;

//namespace GS.Trade.Core
//{
//    public class TradingFactory : ITradingFactory
//    {
//        public ITrade CreateTrade(string ticker, decimal price, int qty, TradeSide side, DateTime dateTime, long tradeNumber)
//        {
//            return new Trade
//            {
//                Ticker = ticker,
//                Price = price,
//                Qty = qty,
//                Side = side,
//                DateTime = dateTime,
//                TradeNumber = tradeNumber
//            };
//        }

//        public IDeal CreateDeal(ITrade openTrade, ITrade closeTrade, int qty, DateTime dealTime)
//        {
//            if (openTrade.Ticker != closeTrade.Ticker)
//                throw new ArgumentException("Trade tickers must match");

//            decimal pnl = CalculatePnL(openTrade, closeTrade, qty);

//            return new Deal
//            {
//                DateTime = dealTime,
//                Ticker = openTrade.Ticker,
//                OpenTradeNumber = openTrade.TradeNumber,
//                CloseTradeNumber = closeTrade.TradeNumber,
//                Qty = qty,
//                Side = openTrade.Side,
//                OpenPrice = openTrade.Price,
//                ClosePrice = closeTrade.Price,
//                PnL = pnl
//            };
//        }

//        private static decimal CalculatePnL(ITrade openTrade, ITrade closeTrade, int qty)
//        {
//            return openTrade.Side == TradeSide.Buy
//                ? (closeTrade.Price - openTrade.Price) * qty
//                : (openTrade.Price - closeTrade.Price) * qty;
//        }
//    }
//}
