// GS.Trade.Core01
// Models.cs

using System;
using System.Collections.Generic;
using System.Linq;
using GS.Trade.Abstractions;

namespace GS.Trade.Core
{
    public class Trade : ITrade
    {
        public required string Ticker { get; set; }
        public decimal Price { get; set; }
        public int Qty { get; set; }
        public TradeSide Side { get; set; }
        public DateTime DateTime { get; set; }
        public long TradeNumber { get; set; }

        public override string ToString()
            => $"Trade #{TradeNumber}: {Ticker} {Side} {Qty} @ {Price:F2}";
    }

    public class Deal : IDeal
    {
        public long Number { get; set; }
        public DateTime DateTime { get; set; }
        public required string Ticker { get; set; }
        public long OpenTradeNumber { get; set; }
        public long CloseTradeNumber { get; set; }
        public int Qty { get; set; }
        public TradeSide Side { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal PnL { get; set; }

        public override string ToString()
            => $"Deal #{Number}: {Ticker} {Side} {Qty} @ {OpenPrice:F2} -> {ClosePrice:F2} | PnL: {PnL:F2}";
    }

    public class PositionSummary : IPositionSummary
    {
        public string Ticker { get; set; } = string.Empty;
        public PositionStatus Status { get; set; }
        public int NetQuantity { get; set; }
        public int OpenTradesCount { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal UnrealizedProfit { get; set; }
        public List<ITrade> OpenBuys { get; set; } = new();
        public List<ITrade> OpenSells { get; set; } = new();

        IReadOnlyList<ITrade> IPositionSummary.OpenBuys => OpenBuys;
        IReadOnlyList<ITrade> IPositionSummary.OpenSells => OpenSells;
        IReadOnlyList<ITrade> IPositionSummary.OpenTrades => OpenBuys.Concat(OpenSells)
            .OrderBy(t => t.DateTime).ThenBy(t => t.TradeNumber).ToList();
    }

    public class StrategySummary : IStrategySummary
    {
        public string Ticker { get; set; } = string.Empty;
        public string StrategyName { get; set; } = string.Empty;
        public PositionStatus Status { get; set; }
        public int NetQuantity { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal RealizedProfit { get; set; }
        public decimal UnrealizedProfit { get; set; }
        public decimal TotalProfit { get; set; }
        public int OpenTradesCount { get; set; }
        public int ClosedDealsCount { get; set; }
    }

    // GS.Trade.Core
    // Добавляем в Models.cs

    public class Tick : ITick
    {
        public required string Ticker { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
        public DateTime DateTime { get; set; }
        public long TickNumber { get; set; }

        public override string ToString()
            => $"Tick #{TickNumber}: {Ticker} @ {Price:F2} Vol: {Volume}";
    }

    public class CandleStick : ICandleStick
    {
        public required string Ticker { get; set; }
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public TimeSpan TimeFrame { get; set; }

        public override string ToString()
            => $"Candle: {Ticker} | O:{Open:F2} H:{High:F2} L:{Low:F2} C:{Close:F2} | Vol:{Volume}";
    }

    public class Order : IOrder
    {
        public required string Ticker { get; set; }
        public decimal Price { get; set; }
        public int Qty { get; set; }
        public TradeSide Side { get; set; }
        public DateTime DateTime { get; set; }
        public long OrderNumber { get; set; }
        public OrderStatus Status { get; set; }
        public string? StrategyName { get; set; }
        public DateTime? ExecutionTime { get; set; }
        public decimal? ExecutionPrice { get; set; }

        public override string ToString()
            => $"Order #{OrderNumber}: {Ticker} {Side} {Qty} @ {Price:F2} | Status: {Status}";
    }
}
