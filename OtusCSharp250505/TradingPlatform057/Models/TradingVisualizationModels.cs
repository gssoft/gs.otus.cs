// Models/TradingVisualizationModels.cs
using System;
using System.Collections.Generic;

namespace TradingPlatform.Visualization
{
    public class TradingEntityBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Ticker { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class VisualTrade : TradingEntityBase
    {
        public string Side { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Volume => Price * Quantity;
    }

    public class VisualDeal : TradingEntityBase
    {
        public long DealNumber { get; set; }
        public string Side { get; set; } = string.Empty;
        public int Qty { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal PnL { get; set; }
    }

    public class VisualOrder : TradingEntityBase
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ExecutedAt { get; set; }
    }

    public class TickerStrategySummary
    {
        public string Ticker { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public int Position { get; set; }
        public decimal OpenPnL { get; set; }
        public decimal ClosedPnL { get; set; }
        public decimal TotalPnL => OpenPnL + ClosedPnL;
        public int OpenTrades { get; set; }
        public int TotalTrades { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
