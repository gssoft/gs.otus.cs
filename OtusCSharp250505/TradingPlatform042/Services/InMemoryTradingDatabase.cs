// Services/InMemoryTradingDatabase.cs
using BusLibrary02.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TradingPlatform.Events;
using TradingPlatform.Visualization;

namespace TradingPlatform.Services
{
    public interface IInMemoryTradingDatabase
    {
        IEnumerable<TickerStrategySummary> GetSummaries();
        PagedResult<VisualTrade> GetTrades(string? ticker = null, string? strategy = null, int page = 1, int pageSize = 20);
        PagedResult<VisualDeal> GetDeals(string? ticker = null, string? strategy = null, int page = 1, int pageSize = 20);
        PagedResult<VisualOrder> GetOrders(string? ticker = null, string? strategy = null, int page = 1, int pageSize = 20);

        // События для SignalR
        event Action<TickerStrategySummary> SummaryUpdated;
        event Action<VisualTrade> TradeAdded;
        event Action<VisualDeal> DealAdded;
        event Action<VisualOrder> OrderAdded;
    }

    public class InMemoryTradingDatabase : BackgroundService, IInMemoryTradingDatabase
    {
        private readonly ILogger<InMemoryTradingDatabase> _logger;
        private readonly IEventHub _eventHub;
        private readonly IDynamicSubscriptionManager _subscriptionManager;
        private readonly List<IDisposable> _subscriptions = new();

        // Коллекции для хранения данных
        private readonly ConcurrentDictionary<string, List<VisualTrade>> _trades = new();
        private readonly ConcurrentDictionary<string, List<VisualDeal>> _deals = new();
        private readonly ConcurrentDictionary<string, List<VisualOrder>> _orders = new();

        // Агрегированные данные по тикеру+стратегии
        private readonly ConcurrentDictionary<string, TickerStrategySummary> _summaries = new();

        // События для обновления UI
        public event Action<TickerStrategySummary>? SummaryUpdated;
        public event Action<VisualTrade>? TradeAdded;
        public event Action<VisualDeal>? DealAdded;
        public event Action<VisualOrder>? OrderAdded;

        public InMemoryTradingDatabase(
            ILogger<InMemoryTradingDatabase> logger,
            IEventHub eventHub,
            IDynamicSubscriptionManager subscriptionManager)
        {
            _logger = logger;
            _eventHub = eventHub;
            _subscriptionManager = subscriptionManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("InMemoryTradingDatabase запущен");

            // Ждем 3 секунды, чтобы остальные сервисы успели запуститься
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            // Подписка на торговые события
            SubscribeToTradingEvents();

            _logger.LogInformation("✅ InMemoryTradingDatabase подписался на события");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Обновляем агрегированные данные каждую секунду
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

                    // Логируем состояние
                    if (_summaries.Count > 0 && DateTime.UtcNow.Second % 10 == 0)
                    {
                        _logger.LogDebug("InMemoryDatabase: {Count} summaries, latest update: {Time}",
                            _summaries.Count, DateTime.UtcNow.ToString("HH:mm:ss"));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в основном цикле InMemoryTradingDatabase");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("InMemoryTradingDatabase остановлен");
        }

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    _logger.LogInformation("InMemoryTradingDatabase запущен");

        //    // Подписка на торговые события
        //    SubscribeToTradingEvents();

        //    // Периодическое обновление PnL
        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        //        }
        //        catch (OperationCanceledException)
        //        {
        //            break;
        //        }
        //    }

        //    _logger.LogInformation("InMemoryTradingDatabase остановлен");
        //}

        private void SubscribeToTradingEvents()
        {
            _logger.LogInformation("🔄 Подписываюсь на торговые события со СТАТИЧЕСКИМИ ключами...");

            try
            {
                // Подписка с ЯВНЫМИ СТАТИЧЕСКИМИ ключами
                _subscriptions.Add(_subscriptionManager.Subscribe<TradeExecutedEvent>(
                    "trade:executed",
                    async (tradeEvent, ct) =>
                    {
                        try
                        {
                            _logger.LogDebug("📈 Получен TradeExecutedEvent: {Symbol} {Side} {Strategy}",
                                tradeEvent.Symbol, tradeEvent.Side, tradeEvent.StrategyName);
                            await ProcessTradeEvent(tradeEvent, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка обработки TradeExecutedEvent");
                        }
                    }));

                _subscriptions.Add(_subscriptionManager.Subscribe<DealClosedEvent>(
                    "deal:closed",
                    async (dealEvent, ct) =>
                    {
                        try
                        {
                            _logger.LogDebug("💰 Получен DealClosedEvent: {Symbol} PnL={PnL}",
                                dealEvent.Symbol, dealEvent.PnL);
                            await ProcessDealEvent(dealEvent, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка обработки DealClosedEvent");
                        }
                    }));

                _subscriptions.Add(_subscriptionManager.Subscribe<OrderCreatedEvent>(
                    "order:created",
                    async (orderEvent, ct) =>
                    {
                        try
                        {
                            _logger.LogDebug("📝 Получен OrderCreatedEvent: {Symbol} {Side}",
                                orderEvent.Symbol, orderEvent.Side);
                            await ProcessOrderEvent(orderEvent, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка обработки OrderCreatedEvent");
                        }
                    }));

                _subscriptions.Add(_subscriptionManager.Subscribe<PositionChangedEvent>(
                    "position:changed",
                    async (positionEvent, ct) =>
                    {
                        try
                        {
                            _logger.LogDebug("📊 Получен PositionChangedEvent: {Symbol} {Strategy} Qty={Quantity}",
                                positionEvent.Symbol, positionEvent.StrategyName, positionEvent.NetQuantity);
                            await ProcessPositionEvent(positionEvent, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка обработки PositionChangedEvent");
                        }
                    }));

                _subscriptions.Add(_subscriptionManager.Subscribe<QuoteGeneratedEvent>(
                    "quote:generated",
                    async (quoteEvent, ct) =>
                    {
                        try
                        {
                            // Логируем каждую 10-ю котировку чтобы не засорять логи
                            if (DateTime.Now.Second % 10 == 0)
                            {
                                _logger.LogDebug("📊 Получена котировка: {Symbol} {Close}",
                                    quoteEvent.Symbol, quoteEvent.Close);
                            }
                            await ProcessQuoteEvent(quoteEvent, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка обработки QuoteGeneratedEvent");
                        }
                    }));

                _logger.LogInformation("✅ Подписка на статические ключи завершена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Ошибка при подписке на события");
            }
        }


        //private void SubscribeToTradingEvents()
        //{
        //    // Подписка на события через существующий EventHub
        //    var tradeSubscription = _subscriptionManager.Subscribe<TradeExecutedEvent>(
        //        async (tradeEvent, ct) =>
        //        {
        //            try
        //            {
        //                await ProcessTradeEvent(tradeEvent, ct);
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Ошибка обработки TradeExecutedEvent");
        //            }
        //        });

        //    var dealSubscription = _subscriptionManager.Subscribe<DealClosedEvent>(
        //        async (dealEvent, ct) =>
        //        {
        //            try
        //            {
        //                await ProcessDealEvent(dealEvent, ct);
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Ошибка обработки DealClosedEvent");
        //            }
        //        });

        //    var orderSubscription = _subscriptionManager.Subscribe<OrderCreatedEvent>(
        //        async (orderEvent, ct) =>
        //        {
        //            try
        //            {
        //                await ProcessOrderEvent(orderEvent, ct);
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Ошибка обработки OrderCreatedEvent");
        //            }
        //        });

        //    var quoteSubscription = _subscriptionManager.Subscribe<QuoteGeneratedEvent>(
        //        async (quoteEvent, ct) =>
        //        {
        //            try
        //            {
        //                await ProcessQuoteEvent(quoteEvent, ct);
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Ошибка обработки QuoteGeneratedEvent");
        //            }
        //        });

        //    var positionSubscription = _subscriptionManager.Subscribe<PositionChangedEvent>(
        //        async (positionEvent, ct) =>
        //        {
        //            try
        //            {
        //                await ProcessPositionEvent(positionEvent, ct);
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Ошибка обработки PositionChangedEvent");
        //            }
        //        });

        //    _subscriptions.Add(tradeSubscription);
        //    _subscriptions.Add(dealSubscription);
        //    _subscriptions.Add(orderSubscription);
        //    _subscriptions.Add(quoteSubscription);
        //    _subscriptions.Add(positionSubscription);

        //    _logger.LogInformation("Подписался на торговые события");
        //}

        private async Task ProcessTradeEvent(TradeExecutedEvent tradeEvent, CancellationToken ct)
        {
            var visualTrade = new VisualTrade
            {
                Ticker = tradeEvent.Symbol,
                Strategy = tradeEvent.StrategyName,
                Side = tradeEvent.Side,
                Price = tradeEvent.Price,
                Quantity = tradeEvent.Quantity,
                Timestamp = tradeEvent.Timestamp,
                Status = "Executed"
            };

            var key = GetKey(tradeEvent.Symbol, tradeEvent.StrategyName);

            // Добавляем сделку
            _trades.AddOrUpdate(key,
                new List<VisualTrade> { visualTrade },
                (k, list) => { list.Add(visualTrade); return list; });

            // Обновляем агрегированные данные
            UpdateSummary(tradeEvent.Symbol, tradeEvent.StrategyName);

            // Вызываем событие
            TradeAdded?.Invoke(visualTrade);

            _logger.LogDebug("Trade added: {Ticker} {Side} {Quantity} @ {Price}",
                visualTrade.Ticker, visualTrade.Side, visualTrade.Quantity, visualTrade.Price);
        }

        private async Task ProcessPositionEvent(PositionChangedEvent positionEvent, CancellationToken ct)
        {
            // Теперь у нас есть стратегия!
            if (string.IsNullOrEmpty(positionEvent.StrategyName))
            {
                _logger.LogWarning("PositionChangedEvent без стратегии для {Symbol}", positionEvent.Symbol);
                return;
            }

            var key = GetKey(positionEvent.Symbol, positionEvent.StrategyName);

            if (!_summaries.TryGetValue(key, out var summary))
            {
                summary = new TickerStrategySummary
                {
                    Ticker = positionEvent.Symbol,
                    Strategy = positionEvent.StrategyName,
                    LastUpdated = DateTime.UtcNow
                };
                _summaries[key] = summary;

                _logger.LogDebug("Создан новый TickerStrategySummary: {Key}", key);
            }

            // Обновляем позицию и PnL
            summary.Position = positionEvent.NetQuantity;
            summary.OpenPnL = positionEvent.UnrealizedPnL;
            summary.LastUpdated = DateTime.UtcNow;

            // Обновляем цену, если она уже есть
            if (summary.CurrentPrice == 0 && _tickerPrices.TryGetValue(positionEvent.Symbol, out var price))
            {
                summary.CurrentPrice = price;
            }

            // Вызываем событие обновления
            SummaryUpdated?.Invoke(summary);

            _logger.LogDebug("Position updated: {Ticker} {Strategy} NetQty={NetQty} PnL={PnL}",
                summary.Ticker, summary.Strategy, summary.Position, summary.OpenPnL);
        }

        private readonly ConcurrentDictionary<string, decimal> _tickerPrices = new();

        private async Task ProcessQuoteEvent(QuoteGeneratedEvent quoteEvent, CancellationToken ct)
        {
            // Сохраняем цену
            _tickerPrices[quoteEvent.Symbol] = quoteEvent.Close;

            // Обновляем все суммарии для этого тикера
            foreach (var (key, summary) in _summaries)
            {
                if (summary.Ticker == quoteEvent.Symbol)
                {
                    summary.CurrentPrice = quoteEvent.Close;
                    summary.LastUpdated = DateTime.UtcNow;

                    // Пересчитываем OpenPnL
                    await UpdateOpenPnLAsync(summary);

                    SummaryUpdated?.Invoke(summary);
                }
            }
        }
        private async Task UpdateOpenPnLAsync(TickerStrategySummary summary)
        {
            var key = GetKey(summary.Ticker, summary.Strategy);

            if (_trades.TryGetValue(key, out var trades))
            {
                var openTrades = trades.Where(t => t.Status == "Open");
                if (summary.CurrentPrice > 0 && openTrades.Any())
                {
                    decimal openPnL = 0;
                    foreach (var trade in openTrades)
                    {
                        var pnl = (summary.CurrentPrice - trade.Price) * trade.Quantity;
                        openPnL += trade.Side == "Buy" ? pnl : -pnl;
                    }
                    summary.OpenPnL = openPnL;
                }
            }
        }

        private async Task ProcessDealEvent(DealClosedEvent dealEvent, CancellationToken ct)
        {
            _logger.LogInformation("🔄 Processing DealClosedEvent: Symbol={Symbol}, Strategy={Strategy}, PnL={PnL}",
                dealEvent.Symbol, dealEvent.StrategyName, dealEvent.PnL);

            // Если StrategyName пустое, попробуем получить из других источников
            if (string.IsNullOrEmpty(dealEvent.StrategyName))
            {
                _logger.LogWarning("⚠️ DealClosedEvent без StrategyName: {Symbol}, Deal #{DealNumber}",
                    dealEvent.Symbol, dealEvent.DealNumber);

                // Попробуем найти стратегию по тикеру
                // Это временное решение
                dealEvent = dealEvent with { StrategyName = "Unknown" };
            }


            var visualDeal = new VisualDeal
            {
                Ticker = dealEvent.Symbol,
                Strategy = dealEvent.StrategyName,
                DealNumber = dealEvent.DealNumber,
                Side = dealEvent.Side,
                Qty = dealEvent.Qty,
                OpenPrice = dealEvent.OpenPrice,
                ClosePrice = dealEvent.ClosePrice,
                PnL = dealEvent.PnL,
                Timestamp = dealEvent.Timestamp
            };

            // Пока не знаем стратегию из DealClosedEvent, используем "Unknown"
            var key = GetKey(dealEvent.Symbol, dealEvent.StrategyName);

            _deals.AddOrUpdate(key,
                new List<VisualDeal> { visualDeal },
                (k, list) => { list.Add(visualDeal); return list; });

            // Обновляем агрегированные данные
            UpdateSummaryForDeal(dealEvent.Symbol, dealEvent.StrategyName, dealEvent.PnL);

            DealAdded?.Invoke(visualDeal);

            _logger.LogDebug("Deal added: {Ticker} PnL={PnL:F2}",
                visualDeal.Ticker, visualDeal.PnL);
        }

        private void UpdateSummaryForDeal(string ticker, string strategy, decimal pnl)
        {
            var key = GetKey(ticker, strategy);

            if (!_summaries.TryGetValue(key, out var summary))
            {
                // Создаем новую запись
                summary = new TickerStrategySummary
                {
                    Ticker = ticker,
                    Strategy = strategy,
                    ClosedPnL = pnl,
                    LastUpdated = DateTime.UtcNow
                };
                _summaries[key] = summary;

                _logger.LogInformation("📊 Created new summary for {Ticker}-{Strategy} with ClosedPnL: {PnL:F2}",
                    ticker, strategy, pnl);
            }
            else
            {
                // Обновляем существующую запись
                summary.ClosedPnL += pnl;
                summary.LastUpdated = DateTime.UtcNow;

                _logger.LogInformation("📊 Updated summary for {Ticker}-{Strategy}: ClosedPnL += {PnL:F2} = {Total:F2}",
                    ticker, strategy, pnl, summary.ClosedPnL);
            }

            SummaryUpdated?.Invoke(summary);
        }

        private async Task ProcessOrderEvent(OrderCreatedEvent orderEvent, CancellationToken ct)
        {
            var visualOrder = new VisualOrder
            {
                Ticker = orderEvent.Symbol,
                Strategy = orderEvent.Status, // В OrderCreatedEvent нет стратегии, используем статус
                OrderNumber = orderEvent.OrderNumber,
                Side = orderEvent.Side,
                Price = orderEvent.Price,
                Quantity = orderEvent.Quantity,
                Status = orderEvent.Status,
                Timestamp = orderEvent.Timestamp
            };

            var key = GetKey(orderEvent.Symbol, "Unknown");

            _orders.AddOrUpdate(key,
                new List<VisualOrder> { visualOrder },
                (k, list) => { list.Add(visualOrder); return list; });

            OrderAdded?.Invoke(visualOrder);

            _logger.LogDebug("Order added: {Ticker} {Side} {Quantity}",
                visualOrder.Ticker, visualOrder.Side, visualOrder.Quantity);
        }

        //private async Task ProcessQuoteEvent(QuoteGeneratedEvent quoteEvent, CancellationToken ct)
        //{
        //    // Обновляем цену во всех суммариях для этого тикера
        //    foreach (var (key, summary) in _summaries)
        //    {
        //        if (summary.Ticker == quoteEvent.Symbol)
        //        {
        //            summary.CurrentPrice = quoteEvent.Close;
        //            summary.LastUpdated = DateTime.UtcNow;

        //            // Пересчитываем OpenPnL если есть открытые позиции
        //            UpdateOpenPnL(summary);

        //            SummaryUpdated?.Invoke(summary);
        //        }
        //    }
        //}

        //private async Task ProcessPositionEvent(PositionChangedEvent positionEvent, CancellationToken ct)
        //{
        //    // Обновляем позицию в суммариях
        //    // Здесь нам нужно знать стратегию, но в PositionChangedEvent её нет
        //    // Можно попробовать найти по тикеру и последним сделкам
        //}

        private void UpdateSummary(string ticker, string strategy)
        {
            var key = GetKey(ticker, strategy);

            if (!_summaries.TryGetValue(key, out var summary))
            {
                summary = new TickerStrategySummary
                {
                    Ticker = ticker,
                    Strategy = strategy,
                    LastUpdated = DateTime.UtcNow
                };
                _summaries[key] = summary;
            }

            // Пересчитываем агрегированные данные
            RecalculateSummary(ticker, strategy, summary);

            summary.LastUpdated = DateTime.UtcNow;
            SummaryUpdated?.Invoke(summary);
        }

        private void RecalculateSummary(string ticker, string strategy, TickerStrategySummary summary)
        {
            var key = GetKey(ticker, strategy);

            if (_trades.TryGetValue(key, out var trades))
            {
                summary.TotalTrades = trades.Count;
                summary.OpenTrades = trades.Count(t => t.Status == "Open");

                // Вычисляем позицию
                summary.Position = trades
                    .Where(t => t.Status == "Open")
                    .Sum(t => t.Side == "Buy" ? t.Quantity : -t.Quantity);
            }

            if (_deals.TryGetValue(key, out var deals))
            {
                summary.ClosedPnL = deals.Sum(d => d.PnL);
            }
        }

        private void UpdateOpenPnL(TickerStrategySummary summary)
        {
            var key = GetKey(summary.Ticker, summary.Strategy);

            if (_trades.TryGetValue(key, out var trades))
            {
                var openTrades = trades.Where(t => t.Status == "Open");
                summary.OpenPnL = openTrades.Sum(t =>
                    (summary.CurrentPrice - t.Price) * t.Quantity *
                    (t.Side == "Buy" ? 1 : -1));
            }
        }

        private string GetKey(string ticker, string strategy)
        {
            return $"{ticker}:{strategy}";
        }

        // Реализация методов интерфейса
        public IEnumerable<TickerStrategySummary> GetSummaries()
        {
            return _summaries.Values
                .OrderBy(s => s.Ticker)
                .ThenBy(s => s.Strategy);
        }

        public PagedResult<VisualTrade> GetTrades(string? ticker = null, string? strategy = null, int page = 1, int pageSize = 20)
        {
            var allTrades = _trades.Values.SelectMany(x => x);

            if (!string.IsNullOrEmpty(ticker))
                allTrades = allTrades.Where(t => t.Ticker == ticker);

            if (!string.IsNullOrEmpty(strategy))
                allTrades = allTrades.Where(t => t.Strategy == strategy);

            var totalCount = allTrades.Count();
            var items = allTrades
                .OrderByDescending(t => t.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<VisualTrade>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public PagedResult<VisualDeal> GetDeals(string? ticker = null, string? strategy = null, int page = 1, int pageSize = 20)
        {
            var allDeals = _deals.Values.SelectMany(x => x);

            if (!string.IsNullOrEmpty(ticker))
                allDeals = allDeals.Where(d => d.Ticker == ticker);

            if (!string.IsNullOrEmpty(strategy))
                allDeals = allDeals.Where(d => d.Strategy == strategy);

            var totalCount = allDeals.Count();
            var items = allDeals
                .OrderByDescending(d => d.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<VisualDeal>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public PagedResult<VisualOrder> GetOrders(string? ticker = null, string? strategy = null, int page = 1, int pageSize = 20)
        {
            var allOrders = _orders.Values.SelectMany(x => x);

            if (!string.IsNullOrEmpty(ticker))
                allOrders = allOrders.Where(o => o.Ticker == ticker);

            if (!string.IsNullOrEmpty(strategy))
                allOrders = allOrders.Where(o => o.Strategy == strategy);

            var totalCount = allOrders.Count();
            var items = allOrders
                .OrderByDescending(o => o.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<VisualOrder>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public override void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();

            base.Dispose();
        }
    }
}
