// Services/StrategiesManagerService.cs

//using BusLibrary02.Core;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using TradingPlatform.Events;
//using TradingPlatform.Services;
//using System.Threading.Tasks;
//using System.Threading;

//namespace TradingPlatform.Services
//{
//    public class StrategiesManagerService : BackgroundService
//    {
//        private readonly ILogger<StrategiesManagerService> _logger;
//        private readonly EventHubTickerManager _tickerManager;
//        private readonly IEventHub _eventHub;
//        private bool _strategiesStarted = false;

//        public StrategiesManagerService(
//            ILogger<StrategiesManagerService> logger,
//            EventHubTickerManager tickerManager,
//            IEventHub eventHub)
//        {
//            _logger = logger;
//            _tickerManager = tickerManager;
//            _eventHub = eventHub;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("🚀 StrategiesManagerService запущен");

//            // Ждем, чтобы другие сервисы успели инициализироваться
//            await Task.Delay(3000, stoppingToken);

//            // Запускаем все стратегии
//            await StartAllStrategies(stoppingToken);

//            // Публикуем системное событие
//            await _eventHub.PublishAsync(new SystemStatusEvent(
//                "StrategiesManager",
//                "Started",
//                $"Запущено стратегий: {GetTotalStrategiesCount()}",
//                DateTime.Now
//            ), stoppingToken);

//            // Мониторинг и поддержание работы стратегий
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    // Проверяем состояние стратегий каждые 30 секунд
//                    await Task.Delay(30000, stoppingToken);

//                    // Можно добавить логику перезапуска стратегий при необходимости
//                    _logger.LogDebug("StrategiesManager: проверка состояния стратегий");
//                }
//                catch (OperationCanceledException)
//                {
//                    break;
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Ошибка в StrategiesManagerService");
//                }
//            }

//            // Останавливаем стратегии при завершении
//            await StopAllStrategies();
//            _logger.LogInformation("🛑 StrategiesManagerService остановлен");
//        }

//        private async Task StartAllStrategies(CancellationToken ct)
//        {
//            if (_strategiesStarted)
//                return;

//            try
//            {
//                var tickers = _tickerManager.GetAllTickers();
//                int totalStrategies = 0;

//                foreach (var ticker in tickers)
//                {
//                    foreach (var strategy in ticker.Strategies)
//                    {
//                        if (strategy is EventHubStrategy eventHubStrategy)
//                        {
//                            // Запускаем стратегию
//                            eventHubStrategy.StartTrading(ct);

//                            _logger.LogDebug(
//                                "Запущена стратегия {StrategyName} для {Ticker}",
//                                eventHubStrategy.StrategyName, ticker.Symbol);

//                            totalStrategies++;
//                        }
//                    }
//                }

//                _strategiesStarted = true;
//                _logger.LogInformation("✅ Запущено {Count} стратегий", totalStrategies);

//                // Публикуем событие о запуске стратегий
//                await _eventHub.PublishAsync(new SystemStatusEvent(
//                    "StrategiesManager",
//                    "StrategiesStarted",
//                    $"Запущено {totalStrategies} стратегий",
//                    DateTime.Now
//                ), ct);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Ошибка при запуске стратегий");
//                throw;
//            }
//        }

//        private Task StopAllStrategies()
//        {
//            try
//            {
//                var tickers = _tickerManager.GetAllTickers();
//                foreach (var ticker in tickers)
//                {
//                    foreach (var strategy in ticker.Strategies)
//                    {
//                        if (strategy is EventHubStrategy eventHubStrategy)
//                        {
//                            eventHubStrategy.StopTrading();
//                        }
//                    }
//                }

//                _strategiesStarted = false;
//                _logger.LogInformation("🛑 Все стратегии остановлены");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Ошибка при остановке стратегий");
//            }

//            return Task.CompletedTask;
//        }

//        private int GetTotalStrategiesCount()
//        {
//            return _tickerManager.GetAllTickers()
//                .Sum(t => t.Strategies.Count);
//        }
//    }
//}