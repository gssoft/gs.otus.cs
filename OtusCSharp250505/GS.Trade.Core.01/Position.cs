// GS.Trade.Core
// Positions.cs

using System;
using System.Collections.Generic;
using System.Linq;
using GS.Trade.Abstractions;

namespace GS.Trade.Core
{
    public class Position : IPosition
    {
        private readonly List<Trade> _openTrades = new();
        private decimal _lastPrice;
        private readonly ITradeStrategy _strategy;
        private readonly ITradingFactory _factory;

        public event Action<IDeal>? DealClosed;

        public Position(ITradeStrategy strategy, ITradingFactory? factory = null)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _factory = factory ?? new TradingFactory();
        }

        public string Ticker => _strategy.Ticker;
        public int NetQuantity => _openTrades.Sum(t => t.Side == TradeSide.Buy ? t.Qty : -t.Qty);
        public bool HasPosition => NetQuantity != 0;

        public decimal LastPrice
        {
            get => _lastPrice;
            set
            {
                _lastPrice = value;
                RecalculateUnrealizedProfit();
            }
        }

        public decimal UnrealizedProfit { get; private set; }

        public PositionStatus Status
        {
            get
            {
                if (!HasPosition) return PositionStatus.Flat;
                return NetQuantity > 0 ? PositionStatus.Long : PositionStatus.Short;
            }
        }

        public void ProcessTrade(ITrade trade)
        {
            ValidateTrade(trade);

            // Создаем внутреннюю копию трейда
            var internalTrade = new Trade
            {
                Ticker = trade.Ticker,
                Price = trade.Price,
                Qty = trade.Qty,
                Side = trade.Side,
                DateTime = trade.DateTime,
                TradeNumber = trade.TradeNumber
            };

            if (internalTrade.Side == TradeSide.Buy)
            {
                ProcessBuyTrade(internalTrade);
            }
            else
            {
                ProcessSellTrade(internalTrade);
            }

            RecalculateUnrealizedProfit();
        }

        public void ClosePosition()
        {
            _openTrades.Clear();
            UnrealizedProfit = 0m;
        }

        public IPositionSummary GetSummary()
        {
            var openBuys = _openTrades.Where(t => t.Side == TradeSide.Buy).Cast<ITrade>().ToList();
            var openSells = _openTrades.Where(t => t.Side == TradeSide.Sell).Cast<ITrade>().ToList();

            return new PositionSummary
            {
                Ticker = Ticker,
                Status = Status,
                NetQuantity = NetQuantity,
                OpenTradesCount = _openTrades.Count,
                CurrentPrice = LastPrice,
                UnrealizedProfit = UnrealizedProfit,
                OpenBuys = openBuys,
                OpenSells = openSells
            };
        }

        public IReadOnlyList<ITrade> GetOpenTrades()
        {
            return _openTrades
                .OrderBy(t => t.DateTime)
                .ThenBy(t => t.TradeNumber)
                .Cast<ITrade>()
                .ToList();
        }

        public override string ToString()
        {
            var summary = GetSummary();
            return $"Position: {summary.Ticker} | " +
                   $"Status: {summary.Status} | " +
                   $"NetQty: {summary.NetQuantity} | " +
                   $"OpenTrades: {summary.OpenTradesCount} | " +
                   $"CurrPrice: {summary.CurrentPrice:F2} | " +
                   $"UnrealizedP&L: {summary.UnrealizedProfit:F2}";
        }

        private void ValidateTrade(ITrade trade)
        {
            if (trade == null)
                throw new ArgumentNullException(nameof(trade));

            if (trade.Qty <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(trade.Qty));

            if (trade.Price <= 0)
                throw new ArgumentException("Price must be positive", nameof(trade.Price));

            if (Ticker != trade.Ticker)
            {
                throw new ArgumentException($"Trade ticker {trade.Ticker} does not match strategy ticker {Ticker}");
            }
        }

        private void ProcessBuyTrade(Trade buyTrade)
        {
            var openSells = _openTrades
                .Where(t => t.Side == TradeSide.Sell)
                .OrderBy(t => t.DateTime)
                .ThenBy(t => t.TradeNumber)
                .ToList();

            int remainingQty = buyTrade.Qty;

            foreach (var sellTrade in openSells)
            {
                if (remainingQty <= 0) break;

                int closeQty = Math.Min(remainingQty, sellTrade.Qty);
                CreateDeal(sellTrade, buyTrade, closeQty);

                remainingQty -= closeQty;

                if (sellTrade.Qty == closeQty)
                {
                    _openTrades.Remove(sellTrade);
                }
                else
                {
                    sellTrade.Qty -= closeQty;
                }
            }

            if (remainingQty > 0)
            {
                var remainingBuy = new Trade
                {
                    Ticker = buyTrade.Ticker,
                    Price = buyTrade.Price,
                    Qty = remainingQty,
                    Side = TradeSide.Buy,
                    DateTime = buyTrade.DateTime,
                    TradeNumber = buyTrade.TradeNumber
                };
                _openTrades.Add(remainingBuy);
            }
        }

        private void ProcessSellTrade(Trade sellTrade)
        {
            var openBuys = _openTrades
                .Where(t => t.Side == TradeSide.Buy)
                .OrderBy(t => t.DateTime)
                .ThenBy(t => t.TradeNumber)
                .ToList();

            int remainingQty = sellTrade.Qty;

            foreach (var buyTrade in openBuys)
            {
                if (remainingQty <= 0) break;

                int closeQty = Math.Min(remainingQty, buyTrade.Qty);
                CreateDeal(buyTrade, sellTrade, closeQty);

                remainingQty -= closeQty;

                if (buyTrade.Qty == closeQty)
                {
                    _openTrades.Remove(buyTrade);
                }
                else
                {
                    buyTrade.Qty -= closeQty;
                }
            }

            if (remainingQty > 0)
            {
                var remainingSell = new Trade
                {
                    Ticker = sellTrade.Ticker,
                    Price = sellTrade.Price,
                    Qty = remainingQty,
                    Side = TradeSide.Sell,
                    DateTime = sellTrade.DateTime,
                    TradeNumber = sellTrade.TradeNumber
                };
                _openTrades.Add(remainingSell);
            }
        }

        private void CreateDeal(Trade openTrade, Trade closeTrade, int qty)
        {
            var deal = _factory.CreateDeal(openTrade, closeTrade, qty, closeTrade.DateTime);

            // Уведомляем стратегию
            _strategy.OnDealClosed(deal);

            // Вызываем событие
            
            DealClosed?.Invoke(deal);
        }

        private void RecalculateUnrealizedProfit()
        {
            UnrealizedProfit = 0m;

            foreach (var trade in _openTrades)
            {
                if (trade.Side == TradeSide.Buy)
                {
                    UnrealizedProfit += (LastPrice - trade.Price) * trade.Qty;
                }
                else
                {
                    UnrealizedProfit += (trade.Price - LastPrice) * trade.Qty;
                }
            }
        }
    }
}
