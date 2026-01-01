// GS.Trade.Abstractions
// ITradingContracts.cs

using System;
using System.Collections.Generic;

namespace GS.Trade.Abstractions
{
    public enum TradeSide { Buy, Sell }
    public enum PositionStatus { Flat, Long, Short }

    public interface ITrade
    {
        string Ticker { get; }
        decimal Price { get; }
        int Qty { get; }
        TradeSide Side { get; }
        DateTime DateTime { get; }
        long TradeNumber { get; }
    }

    public interface IDeal
    {
        long Number { get; set; }
        DateTime DateTime { get; }
        string Ticker { get; }
        long OpenTradeNumber { get; }
        long CloseTradeNumber { get; }
        int Qty { get; }
        TradeSide Side { get; }
        decimal OpenPrice { get; }
        decimal ClosePrice { get; }
        decimal PnL { get; }
    }

    public interface IPositionSummary
    {
        string Ticker { get; }
        PositionStatus Status { get; }
        int NetQuantity { get; }
        int OpenTradesCount { get; }
        decimal CurrentPrice { get; }
        decimal UnrealizedProfit { get; }
        IReadOnlyList<ITrade> OpenBuys { get; }
        IReadOnlyList<ITrade> OpenSells { get; }
        IReadOnlyList<ITrade> OpenTrades { get; }
    }

    public interface IStrategySummary
    {
        string Ticker { get; }
        string StrategyName { get; }
        PositionStatus Status { get; }
        int NetQuantity { get; }
        decimal CurrentPrice { get; }
        decimal RealizedProfit { get; }
        decimal UnrealizedProfit { get; }
        decimal TotalProfit { get; }
        int OpenTradesCount { get; }
        int ClosedDealsCount { get; }
    }

    public interface IPortfolioSummary
    {
        int TotalStrategies { get; }
        int ActiveStrategies { get; }
        decimal TotalRealizedProfit { get; }
        decimal TotalUnrealizedProfit { get; }
        decimal TotalProfit { get; }
        IReadOnlyDictionary<string, int> StrategiesByTicker { get; }
    }

    //public interface ITradeStrategy
    //{
    //    string Ticker { get; }
    //    string StrategyName { get; }
    //    decimal RealizedProfit { get; }
    //    IReadOnlyList<IDeal> ClosedDeals { get; }
    //    IPosition Position { get; }

    //    void ProcessTrade(ITrade trade);
    //    void UpdateMarketPrice(decimal price);
    //    void CloseAllPositions();
    //    IStrategySummary GetStrategySummary();
    //    void OnDealClosed(IDeal deal);
    //}


    public interface IPosition
    {
        string Ticker { get; }
        int NetQuantity { get; }
        bool HasPosition { get; }
        decimal LastPrice { get; set; }
        decimal UnrealizedProfit { get; }
        PositionStatus Status { get; }

        void ProcessTrade(ITrade trade);
        void ClosePosition();
        IPositionSummary GetSummary();
        IReadOnlyList<ITrade> GetOpenTrades();

        // Событие для уведомления о закрытии сделки
        event Action<IDeal> DealClosed;
    }

    public interface IPortfolio<TStrategy> where TStrategy : ITradeStrategy
    {
        IReadOnlyDictionary<string, TStrategy> Strategies { get; }

        void AddStrategy(TStrategy strategy);
        void RemoveStrategy(string strategyKey);
        TStrategy? GetStrategy(string strategyKey);
        void ProcessTrade(string strategyKey, ITrade trade);
        void UpdateMarketPrice(string ticker, decimal price);

        IPortfolioSummary GetPortfolioSummary();
        Dictionary<string, List<IStrategySummary>> GetDetailedSummary();
    }

    // Фабрика для создания торговых объектов
    //public interface ITradingFactory
    //{
    //    ITrade CreateTrade(string ticker, decimal price, int qty, TradeSide side, DateTime dateTime, long tradeNumber);
    //    IDeal CreateDeal(ITrade openTrade, ITrade closeTrade, int qty, DateTime dealTime);
    //}

    public interface IRandomTradeStrategy : ITradeStrategy
    {
        void StartTrading(CancellationToken cancellationToken = default);
        void StopTrading();
    }

    // GS.Trade.Abstractions
    // Добавляем в конец файла ITradingContracts.cs

    public interface ITick
    {
        string Ticker { get; }
        decimal Price { get; }
        decimal Volume { get; }
        DateTime DateTime { get; }
        long TickNumber { get; }
    }

    public interface ICandleStick
    {
        string Ticker { get; }
        DateTime OpenTime { get; }
        DateTime CloseTime { get; }
        decimal Open { get; }
        decimal High { get; }
        decimal Low { get; }
        decimal Close { get; }
        decimal Volume { get; }
        TimeSpan TimeFrame { get; }
    }

    public interface IOrder
    {
        string Ticker { get; }
        decimal Price { get; }
        int Qty { get; }
        TradeSide Side { get; }
        DateTime DateTime { get; }
        long OrderNumber { get; }
        OrderStatus Status { get; }
        string? StrategyName { get; }
        DateTime? ExecutionTime { get; }
        decimal? ExecutionPrice { get; }
    }

    public enum OrderStatus
    {
        Pending,
        Filled,
        PartiallyFilled,
        Cancelled,
        Rejected
    }

    // Расширяем ITradeStrategy
    public interface ITradeStrategy
    {
        string Ticker { get; }
        string StrategyName { get; }
        decimal RealizedProfit { get; }
        IReadOnlyList<IDeal> ClosedDeals { get; }
        IReadOnlyList<ITrade> AllTrades { get; } // Новое свойство
        IReadOnlyList<IOrder> Orders { get; } // Новое свойство
        IPosition Position { get; }

        void ProcessTrade(ITrade trade);
        void ProcessTick(ITick tick); // Новая функция
        void ProcessCandle(ICandleStick candle); // Новая функция
        void UpdateMarketPrice(decimal price);
        void CloseAllPositions();
        IStrategySummary GetStrategySummary();
        void OnDealClosed(IDeal deal);

        // Новые методы для экспорта данных
        IReadOnlyList<ITrade> GetTrades();
        IReadOnlyList<IDeal> GetDeals();
        IPositionSummary GetTradePosition();
        decimal GetRealizedProfit();
        decimal GetUnrealizedProfit();
    }

    // Расширяем ITradingFactory
    public interface ITradingFactory
    {
        ITrade CreateTrade(string ticker, decimal price, int qty, TradeSide side, DateTime dateTime, long tradeNumber);
        IDeal CreateDeal(ITrade openTrade, ITrade closeTrade, int qty, DateTime dealTime);
        ITick CreateTick(string ticker, decimal price, decimal volume, DateTime dateTime, long tickNumber);
        ICandleStick CreateCandle(string ticker, DateTime openTime, DateTime closeTime, decimal open,
            decimal high, decimal low, decimal close, decimal volume, TimeSpan timeFrame);
        IOrder CreateOrder(string ticker, decimal price, int qty, TradeSide side, DateTime dateTime,
            long orderNumber, string strategyName);
    }
}
