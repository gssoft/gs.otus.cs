// Events/TradingEvents.cs

// Events/TradingEvents.cs
using BusLibrary02.Core;

namespace TradingPlatform.Events
{
    // Все ключи делаем СТАТИЧЕСКИМИ без тикера в ключе!

    [MessageKey("quote:generated")]
    public record QuoteGeneratedEvent(
        string Symbol,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume,
        DateTime Timestamp
    ) : MessageBase("quote:generated");

    [MessageKey("strategy:tick")]
    public record StrategyTickEvent(
        string Symbol,
        decimal Price,
        long Volume,
        DateTime Timestamp,
        long TickId
    ) : MessageBase("strategy:tick");

    [MessageKey("strategy:signal")]
    public record StrategySignalEvent(
        string Symbol,
        string StrategyName,
        string Signal,
        decimal Price,
        int Quantity,
        DateTime Timestamp
    ) : MessageBase("strategy:signal");

    [MessageKey("trade:executed")]
    public record TradeExecutedEvent(
        string Symbol,
        string Side,
        decimal Price,
        int Quantity,
        string StrategyName,
        DateTime Timestamp
    ) : MessageBase("trade:executed");

    [MessageKey("position:changed")]
    public record PositionChangedEvent(
        string Symbol,
        string StrategyName,      // ДОБАВИЛИ СТРАТЕГИЮ!
        int NetQuantity,
        decimal UnrealizedPnL,
        string Status,
        DateTime Timestamp
    ) : MessageBase("position:changed");

    [MessageKey("order:created")]
    public record OrderCreatedEvent(
        string Symbol,
        string Side,
        decimal Price,
        int Quantity,
        string Status,
        string OrderNumber,
        DateTime Timestamp
    ) : MessageBase("order:created");

    [MessageKey("deal:closed")]
    public record DealClosedEvent(
        string Symbol,
        string StrategyName,
        long DealNumber,
        string Side,
        int Qty,
        decimal OpenPrice,
        decimal ClosePrice,
        decimal PnL,
        DateTime Timestamp
    ) : MessageBase("deal:closed");

    [MessageKey("chart:update")]
    public record ChartUpdateEvent(
        string Symbol,
        DateTime Timestamp
    ) : MessageBase("chart:update");

    [MessageKey("system:status")]
    public record SystemStatusEvent(
        string Component,
        string Status,
        string Message,
        DateTime Timestamp
    ) : MessageBase("system:status");
}

// -------- 25.12.28 ---------
// Базовые события торговой платформы
//[MessageKey("quote:generated")]
//public record QuoteGeneratedEvent(
//    string Symbol,
//    decimal Open,
//    decimal High,
//    decimal Low,
//    decimal Close,
//    long Volume,
//    DateTime Timestamp
//) : MessageBase("quote:generated");

//[MessageKey("strategy:tick")]
//public record StrategyTickEvent(
//    string Symbol,
//    decimal Price,
//    long Volume,
//    DateTime Timestamp,
//    long TickId
//) : MessageBase($"strategy:tick:{Symbol}");

//[MessageKey("strategy:signal")]
//public record StrategySignalEvent(
//    string Symbol,
//    string StrategyName,
//    string Signal,
//    decimal Price,
//    int Quantity,
//    DateTime Timestamp
//) : MessageBase($"strategy:signal:{Symbol}:{StrategyName}");

//[MessageKey("trade:executed")]
//public record TradeExecutedEvent(
//    string Symbol,
//    string Side,
//    decimal Price,
//    int Quantity,
//    string StrategyName,
//    DateTime Timestamp
//) : MessageBase($"trade:{Symbol}");

//[MessageKey("position:changed")]
//public record PositionChangedEvent(
//    string Symbol,
//    int NetQuantity,
//    decimal UnrealizedPnL,
//    string Status,
//    DateTime Timestamp
//) : MessageBase($"position:{Symbol}");

//[MessageKey("order:created")]
//public record OrderCreatedEvent(
//    string Symbol,
//    string Side,
//    decimal Price,
//    int Quantity,
//    string Status,
//    string OrderNumber,
//    DateTime Timestamp
//) : MessageBase($"order:{Symbol}");

//[MessageKey("deal:closed")]
//public record DealClosedEvent(
//    string Symbol,
//    long DealNumber,
//    string Side,
//    int Qty,
//    decimal OpenPrice,
//    decimal ClosePrice,
//    decimal PnL,
//    DateTime Timestamp
//) : MessageBase($"deal:{Symbol}");

//[MessageKey("chart:update")]
//public record ChartUpdateEvent(
//    string Symbol,
//    DateTime Timestamp
//) : MessageBase($"chart:update:{Symbol}");

//[MessageKey("system:status")]
//public record SystemStatusEvent(
//    string Component,
//    string Status,
//    string Message,
//    DateTime Timestamp
//) : MessageBase($"system:{Component}");
// -------
//using BusLibrary02.Core;

//namespace TradingPlatform.Events;

//// Базовые события торговой платформы
//[MessageKey("quote:generated")]
//public record QuoteGeneratedEvent(
//    string Symbol,
//    decimal Open,
//    decimal High,
//    decimal Low,
//    decimal Close,
//    long Volume,
//    DateTime Timestamp
//) : MessageBase($"quote:{Symbol}");

//[MessageKey("strategy:tick")]
//public record StrategyTickEvent(
//    string Symbol,
//    decimal Price,
//    long Volume,
//    DateTime Timestamp,
//    long TickId
//) : MessageBase($"strategy:tick:{Symbol}");

//[MessageKey("strategy:signal")]
//public record StrategySignalEvent(
//    string Symbol,
//    string StrategyName,
//    string Signal,
//    decimal Price,
//    int Quantity,
//    DateTime Timestamp
//) : MessageBase($"strategy:signal:{Symbol}:{StrategyName}");

//[MessageKey("trade:executed")]
//public record TradeExecutedEvent(
//    string Symbol,
//    string Side,
//    decimal Price,
//    int Quantity,
//    string StrategyName,
//    DateTime Timestamp
//) : MessageBase($"trade:{Symbol}");

//[MessageKey("position:changed")]
//public record PositionChangedEvent(
//    string Symbol,
//    int NetQuantity,
//    decimal UnrealizedPnL,
//    string Status,
//    DateTime Timestamp
//) : MessageBase($"position:{Symbol}");

//[MessageKey("order:created")]
//public record OrderCreatedEvent(
//    string Symbol,
//    string Side,
//    decimal Price,
//    int Quantity,
//    string Status,
//    string OrderNumber,
//    DateTime Timestamp
//) : MessageBase($"order:{Symbol}");

//[MessageKey("deal:closed")]
//public record DealClosedEvent(
//    string Symbol,
//    long DealNumber,
//    string Side,
//    int Qty,
//    decimal OpenPrice,
//    decimal ClosePrice,
//    decimal PnL,
//    DateTime Timestamp
//) : MessageBase($"deal:{Symbol}");

//[MessageKey("chart:update")]
//public record ChartUpdateEvent(
//    string Symbol,
//    DateTime Timestamp
//) : MessageBase($"chart:update:{Symbol}");

//[MessageKey("system:status")]
//public record SystemStatusEvent(
//    string Component,
//    string Status,
//    string Message,
//    DateTime Timestamp
//) : MessageBase($"system:{Component}");