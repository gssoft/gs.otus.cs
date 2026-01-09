// Services/InMemoryLogDatabase.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingPlatform.Visualization;

namespace TradingPlatform.Services
{
    public interface IInMemoryLogDatabase
    {
        IEnumerable<TradingLog> GetLogs(
            string? ticker = null,
            string? strategy = null,
            string? level = null,
            string? category = null,
            int page = 1,
            int pageSize = 50);

        PagedResult<TradingLog> GetPagedLogs(
            string? ticker = null,
            string? strategy = null,
            string? level = null,
            string? category = null,
            int page = 1,
            int pageSize = 50);

        int GetTotalCount();
        void AddLog(TradingLog log);
        void AddTradeLog(string ticker, string strategy, string message, string formattedMessage = "");
        void AddDealLog(string ticker, string strategy, string message, string formattedMessage = "");
        void AddSystemLog(string category, string level, string message, string formattedMessage = "");

        event Action<TradingLog> LogAdded;
    }

    public class InMemoryLogDatabase : BackgroundService, IInMemoryLogDatabase
    {
        private readonly ILogger<InMemoryLogDatabase> _logger;
        private readonly ConcurrentQueue<TradingLog> _logs = new();
        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);
        private readonly object _cleanupLock = new();
        private DateTime _lastCleanup = DateTime.UtcNow;
        private const int MAX_LOGS = 10000;

        public event Action<TradingLog>? LogAdded;

        public InMemoryLogDatabase(ILogger<InMemoryLogDatabase> logger)
        {
            _logger = logger;
        }

        public void AddLog(TradingLog log)
        {
            try
            {
                if (_logs.Count >= MAX_LOGS)
                {
                    _logs.TryDequeue(out _);
                }

                _logs.Enqueue(log);
                LogAdded?.Invoke(log);

                if (_logs.Count % 100 == 0)
                {
                    _logger.LogDebug("Log database: {Count} records", _logs.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении лога");
            }
        }

        public void AddTradeLog(string ticker, string strategy, string message, string formattedMessage = "")
        {
            var log = new TradingLog
            {
                Ticker = ticker,
                Strategy = strategy,
                Level = "Information",
                Category = "Trade",
                Message = message,
                FormattedMessage = string.IsNullOrEmpty(formattedMessage) ? message : formattedMessage,
                Timestamp = DateTime.UtcNow
            };
            AddLog(log);
        }

        public void AddDealLog(string ticker, string strategy, string message, string formattedMessage = "")
        {
            var log = new TradingLog
            {
                Ticker = ticker,
                Strategy = strategy,
                Level = "Information",
                Category = "Deal",
                Message = message,
                FormattedMessage = string.IsNullOrEmpty(formattedMessage) ? message : formattedMessage,
                Timestamp = DateTime.UtcNow
            };
            AddLog(log);
        }

        public void AddSystemLog(string category, string level, string message, string formattedMessage = "")
        {
            var log = new TradingLog
            {
                Category = category,
                Level = level,
                Message = message,
                FormattedMessage = string.IsNullOrEmpty(formattedMessage) ? message : formattedMessage,
                Timestamp = DateTime.UtcNow
            };
            AddLog(log);
        }

        private void CleanupOldLogs()
        {
            lock (_cleanupLock)
            {
                try
                {
                    var cutoffTime = DateTime.UtcNow - _retentionPeriod;
                    int removedCount = 0;
                    var recentLogs = new List<TradingLog>();

                    while (_logs.TryDequeue(out var log))
                    {
                        if (log.Timestamp >= cutoffTime)
                        {
                            recentLogs.Add(log);
                        }
                        else
                        {
                            removedCount++;
                        }
                    }

                    foreach (var log in recentLogs.OrderBy(l => l.Timestamp))
                    {
                        _logs.Enqueue(log);
                    }

                    _lastCleanup = DateTime.UtcNow;

                    if (removedCount > 0)
                    {
                        _logger.LogDebug("Очищено {Count} старых логов", removedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при очистке логов");
                }
            }
        }

        public IEnumerable<TradingLog> GetLogs(
            string? ticker = null,
            string? strategy = null,
            string? level = null,
            string? category = null,
            int page = 1,
            int pageSize = 50)
        {
            var logs = _logs.AsEnumerable().OrderByDescending(l => l.Timestamp);

            if (!string.IsNullOrEmpty(ticker))
                logs = (IOrderedEnumerable<TradingLog>)logs.Where(l => l.Ticker == ticker);

            if (!string.IsNullOrEmpty(strategy))
                logs = (IOrderedEnumerable<TradingLog>)logs.Where(l => l.Strategy == strategy);

            if (!string.IsNullOrEmpty(level))
                logs = (IOrderedEnumerable<TradingLog>)logs.Where(l => l.Level == level);

            if (!string.IsNullOrEmpty(category))
                logs = (IOrderedEnumerable<TradingLog>)logs.Where(l => l.Category == category);

            if (page > 0 && pageSize > 0)
            {
                logs = (IOrderedEnumerable<TradingLog>)logs.Skip((page - 1) * pageSize).Take(pageSize);
            }

            return logs.ToList();
        }

        public PagedResult<TradingLog> GetPagedLogs(
            string? ticker = null,
            string? strategy = null,
            string? level = null,
            string? category = null,
            int page = 1,
            int pageSize = 50)
        {
            var allLogs = _logs.AsEnumerable();

            if (!string.IsNullOrEmpty(ticker))
                allLogs = allLogs.Where(l => l.Ticker == ticker);

            if (!string.IsNullOrEmpty(strategy))
                allLogs = allLogs.Where(l => l.Strategy == strategy);

            if (!string.IsNullOrEmpty(level))
                allLogs = allLogs.Where(l => l.Level == level);

            if (!string.IsNullOrEmpty(category))
                allLogs = allLogs.Where(l => l.Category == category);

            var totalCount = allLogs.Count();

            var items = allLogs
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<TradingLog>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public int GetTotalCount() => _logs.Count;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📝 InMemoryLogDatabase запущен");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CleanupOldLogs();

                    if (DateTime.UtcNow.Minute % 30 == 0 && DateTime.UtcNow.Second < 5)
                    {
                        _logger.LogInformation("📊 Статистика логов: {Count} записей", _logs.Count);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в InMemoryLogDatabase");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("📝 InMemoryLogDatabase остановлен");
        }
    }
}


//// Services/InMemoryLogDatabase.cs
//using System.Collections.Concurrent;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using TradingPlatform.Visualization;

//namespace TradingPlatform.Services
//{
//    public interface IInMemoryLogDatabase
//    {
//        IEnumerable<TradingLog> GetLogs(
//            string? ticker = null,
//            string? strategy = null,
//            string? level = null,
//            string? category = null,
//            int page = 1,
//            int pageSize = 50);

//        PagedResult<TradingLog> GetPagedLogs(
//            string? ticker = null,
//            string? strategy = null,
//            string? level = null,
//            string? category = null,
//            int page = 1,
//            int pageSize = 50);

//        int GetTotalCount();
//        void AddLog(TradingLog log);

//        event Action<TradingLog> LogAdded;
//    }

//    public class InMemoryLogDatabase : BackgroundService, IInMemoryLogDatabase
//    {
//        private readonly ILogger<InMemoryLogDatabase> _logger;
//        private ConcurrentQueue<TradingLog> _logs = new();
//        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);
//        private readonly object _cleanupLock = new();
//        private DateTime _lastCleanup = DateTime.UtcNow;
//        private const int MAX_LOGS = 10000; // Максимум 10,000 записей

//        public event Action<TradingLog>? LogAdded;

//        public InMemoryLogDatabase(ILogger<InMemoryLogDatabase> logger)
//        {
//            _logger = logger;
//            _logger.LogInformation("📝 InMemoryLogDatabase создан (хранение 24 часа, максимум {MaxLogs} записей)", MAX_LOGS);
//        }

//        public void AddLog(TradingLog log)
//        {
//            try
//            {
//                // Ограничиваем количество логов
//                if (_logs.Count >= MAX_LOGS)
//                {
//                    _logs.TryDequeue(out _); // Удаляем старую запись
//                }

//                _logs.Enqueue(log);

//                // Периодическая очистка старых логов
//                if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromMinutes(5))
//                {
//                    CleanupOldLogs();
//                }

//                LogAdded?.Invoke(log);

//                // Логируем только каждую 100-ю запись для отладки
//                if (_logs.Count % 100 == 0)
//                {
//                    _logger.LogDebug("Log database: {Count} records", _logs.Count);
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Ошибка при добавлении лога");
//            }
//        }

//        private void CleanupOldLogs()
//        {
//            lock (_cleanupLock)
//            {
//                try
//                {
//                    var cutoffTime = DateTime.UtcNow - _retentionPeriod;
//                    int removedCount = 0;

//                    // Создаем временный список для актуальных логов
//                    var recentLogs = new List<TradingLog>();

//                    while (_logs.TryDequeue(out var log))
//                    {
//                        if (log.Timestamp >= cutoffTime)
//                        {
//                            recentLogs.Add(log);
//                        }
//                        else
//                        {
//                            removedCount++;
//                        }
//                    }

//                    // Возвращаем актуальные логи обратно
//                    foreach (var log in recentLogs.OrderBy(l => l.Timestamp))
//                    {
//                        _logs.Enqueue(log);
//                    }

//                    _lastCleanup = DateTime.UtcNow;

//                    if (removedCount > 0)
//                    {
//                        _logger.LogDebug("Очищено {Count} старых логов (старше 24 часов)", removedCount);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Ошибка при очистке старых логов");
//                }
//            }
//        }

//        public IEnumerable<TradingLog> GetLogs(
//            string? ticker = null,
//            string? strategy = null,
//            string? level = null,
//            string? category = null,
//            int page = 1,
//            int pageSize = 50)
//        {
//            var logs = _logs.AsEnumerable();

//            // Фильтруем логи (новые сверху)
//            logs = logs.OrderByDescending(l => l.Timestamp);

//            if (!string.IsNullOrEmpty(ticker))
//                logs = logs.Where(l => l.Ticker == ticker);

//            if (!string.IsNullOrEmpty(strategy))
//                logs = logs.Where(l => l.Strategy == strategy);

//            if (!string.IsNullOrEmpty(level))
//                logs = logs.Where(l => l.Level == level);

//            if (!string.IsNullOrEmpty(category))
//                logs = logs.Where(l => l.Category == category);

//            // Пагинация
//            if (page > 0 && pageSize > 0)
//            {
//                logs = logs.Skip((page - 1) * pageSize).Take(pageSize);
//            }

//            return logs.ToList();
//        }

//        public PagedResult<TradingLog> GetPagedLogs(
//            string? ticker = null,
//            string? strategy = null,
//            string? level = null,
//            string? category = null,
//            int page = 1,
//            int pageSize = 50)
//        {
//            var allLogs = _logs.AsEnumerable();

//            // Фильтрация
//            if (!string.IsNullOrEmpty(ticker))
//                allLogs = allLogs.Where(l => l.Ticker == ticker);

//            if (!string.IsNullOrEmpty(strategy))
//                allLogs = allLogs.Where(l => l.Strategy == strategy);

//            if (!string.IsNullOrEmpty(level))
//                allLogs = allLogs.Where(l => l.Level == level);

//            if (!string.IsNullOrEmpty(category))
//                allLogs = allLogs.Where(l => l.Category == category);

//            var totalCount = allLogs.Count();

//            // Сортировка (новые сверху) и пагинация
//            var items = allLogs
//                .OrderByDescending(l => l.Timestamp)
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .ToList();

//            return new PagedResult<TradingLog>
//            {
//                Items = items,
//                PageNumber = page,
//                PageSize = pageSize,
//                TotalCount = totalCount
//            };
//        }

//        public int GetTotalCount()
//        {
//            return _logs.Count;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("📝 InMemoryLogDatabase запущен");

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    // Периодическая очистка старых логов
//                    CleanupOldLogs();

//                    // Логируем статистику каждые 30 минут
//                    if (DateTime.UtcNow.Minute % 30 == 0 && DateTime.UtcNow.Second < 5)
//                    {
//                        _logger.LogInformation("📊 Статистика логов: {Count} записей (макс {MaxLogs}, храним 24 часа)",
//                            _logs.Count, MAX_LOGS);
//                    }

//                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Раз в 5 минут
//                }
//                catch (OperationCanceledException)
//                {
//                    break;
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Ошибка в основном цикле InMemoryLogDatabase");
//                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
//                }
//            }

//            _logger.LogInformation("📝 InMemoryLogDatabase остановлен");
//        }
//    }
//}

//// Services/InMemoryLogDatabase.cs
//using System.Collections.Concurrent;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using TradingPlatform.Visualization;

//namespace TradingPlatform.Services
//{
//    public interface IInMemoryLogDatabase
//    {
//        IEnumerable<TradingLog> GetLogs(
//            string? ticker = null,
//            string? strategy = null,
//            string? level = null,
//            string? category = null,
//            int page = 1,
//            int pageSize = 50);

//        PagedResult<TradingLog> GetPagedLogs(
//            string? ticker = null,
//            string? strategy = null,
//            string? level = null,
//            string? category = null,
//            int page = 1,
//            int pageSize = 50);

//        int GetTotalCount();
//        void AddLog(TradingLog log);
//        void AddLog(string message, string? ticker = null, string? strategy = null);

//        event Action<TradingLog> LogAdded;
//    }

//    public class InMemoryLogDatabase : BackgroundService, IInMemoryLogDatabase
//    {
//        private readonly ILogger<InMemoryLogDatabase> _logger;
//        private readonly ConcurrentQueue<TradingLog> _logs = new();
//        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);
//        private readonly object _cleanupLock = new();
//        private DateTime _lastCleanup = DateTime.UtcNow;

//        public event Action<TradingLog>? LogAdded;

//        public InMemoryLogDatabase(ILogger<InMemoryLogDatabase> logger)
//        {
//            _logger = logger;
//            _logger.LogInformation("📝 InMemoryLogDatabase создан (хранение 24 часа)");
//        }

//        public void AddLog(TradingLog log)
//        {
//            try
//            {
//                _logs.Enqueue(log);

//                // Проверяем, не пора ли очистить старые логи
//                if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromMinutes(5))
//                {
//                    CleanupOldLogs();
//                }

//                // Ограничиваем максимальное количество логов (на всякий случай)
//                while (_logs.Count > 100000) // Максимум 100к записей
//                {
//                    _logs.TryDequeue(out _);
//                }

//                LogAdded?.Invoke(log);

//                _logger.LogTrace("Log added: {Timestamp} {Ticker} {Strategy} {Message}",
//                    log.Timestamp.ToString("HH:mm:ss"), log.Ticker, log.Strategy,
//                    log.Message.Length > 50 ? log.Message.Substring(0, 50) + "..." : log.Message);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Ошибка при добавлении лога");
//            }
//        }

//        public void AddLog(string message, string? ticker = null, string? strategy = null)
//        {
//            var log = new TradingLog
//            {
//                Timestamp = DateTime.UtcNow,
//                Ticker = ticker ?? string.Empty,
//                Strategy = strategy ?? string.Empty,
//                Message = CleanMessage(message),
//                FormattedMessage = message,
//                Level = TradingLog.DetermineLevel(message),
//                Category = TradingLog.DetermineCategory(message)
//            };

//            AddLog(log);
//        }

//        private string CleanMessage(string message)
//        {
//            // Убираем эмодзи и лишние пробелы для чистого текста
//            return message
//                .Replace("📊", "TRADE")
//                .Replace("💰", "DEAL")
//                .Replace("📈", "POSITION")
//                .Replace("📝", "ORDER")
//                .Replace("🔄", "CYCLE")
//                .Replace("✅", "OK")
//                .Replace("❌", "ERROR")
//                .Replace("⚠️", "WARNING")
//                .Replace("🚀", "START")
//                .Replace("🛑", "STOP")
//                .Replace("🔴", "SELL")
//                .Replace("🟢", "BUY")
//                .Replace("⚪", "HOLD")
//                .Replace("📉", "SHORT")
//                .Replace("📈", "LONG")
//                .Replace("➡️", "FLAT")
//                .Replace("↑", "UP")
//                .Replace("↓", "DOWN")
//                .Replace("→", "FLAT")
//                .Replace("🌐", "NETWORK")
//                .Replace("📄", "FILE")
//                .Replace("📋", "SUMMARY")
//                .Replace("⚡", "FAST")
//                .Replace("📡", "SIGNALR")
//                .Replace("🧪", "TEST")
//                .Replace("🔌", "API")
//                .Replace("🔗", "LINK")
//                .Replace("📁", "FOLDER")
//                .Replace("ℹ️", "INFO")
//                .Trim();
//        }

//        private void CleanupOldLogs()
//        {
//            lock (_cleanupLock)
//            {
//                try
//                {
//                    var cutoffTime = DateTime.UtcNow - _retentionPeriod;
//                    int removedCount = 0;

//                    // Создаем временный список для хранения актуальных логов
//                    var recentLogs = new List<TradingLog>();

//                    while (_logs.TryDequeue(out var log))
//                    {
//                        if (log.Timestamp >= cutoffTime)
//                        {
//                            recentLogs.Add(log);
//                        }
//                        else
//                        {
//                            removedCount++;
//                        }
//                    }

//                    // Возвращаем актуальные логи обратно в очередь
//                    foreach (var log in recentLogs.OrderBy(l => l.Timestamp))
//                    {
//                        _logs.Enqueue(log);
//                    }

//                    _lastCleanup = DateTime.UtcNow;

//                    if (removedCount > 0)
//                    {
//                        _logger.LogDebug("Очищено {Count} старых логов (старше 24 часов)", removedCount);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Ошибка при очистке старых логов");
//                }
//            }
//        }

//        public IEnumerable<TradingLog> GetLogs(
//            string? ticker = null,
//            string? strategy = null,
//            string? level = null,
//            string? category = null,
//            int page = 1,
//            int pageSize = 50)
//        {
//            var logs = _logs.AsEnumerable();

//            // Фильтруем логи (новые сверху)
//            logs = logs.OrderByDescending(l => l.Timestamp);

//            if (!string.IsNullOrEmpty(ticker))
//                logs = logs.Where(l => l.Ticker == ticker);

//            if (!string.IsNullOrEmpty(strategy))
//                logs = logs.Where(l => l.Strategy == strategy);

//            if (!string.IsNullOrEmpty(level))
//                logs = logs.Where(l => l.Level == level);

//            if (!string.IsNullOrEmpty(category))
//                logs = logs.Where(l => l.Category == category);

//            // Пагинация
//            if (page > 0 && pageSize > 0)
//            {
//                logs = logs.Skip((page - 1) * pageSize).Take(pageSize);
//            }

//            return logs.ToList();
//        }

//        public PagedResult<TradingLog> GetPagedLogs(
//            string? ticker = null,
//            string? strategy = null,
//            string? level = null,
//            string? category = null,
//            int page = 1,
//            int pageSize = 50)
//        {
//            var allLogs = _logs.AsEnumerable();

//            // Фильтрация
//            if (!string.IsNullOrEmpty(ticker))
//                allLogs = allLogs.Where(l => l.Ticker == ticker);

//            if (!string.IsNullOrEmpty(strategy))
//                allLogs = allLogs.Where(l => l.Strategy == strategy);

//            if (!string.IsNullOrEmpty(level))
//                allLogs = allLogs.Where(l => l.Level == level);

//            if (!string.IsNullOrEmpty(category))
//                allLogs = allLogs.Where(l => l.Category == category);

//            var totalCount = allLogs.Count();

//            // Сортировка (новые сверху) и пагинация
//            var items = allLogs
//                .OrderByDescending(l => l.Timestamp)
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .ToList();

//            return new PagedResult<TradingLog>
//            {
//                Items = items,
//                PageNumber = page,
//                PageSize = pageSize,
//                TotalCount = totalCount
//            };
//        }

//        public int GetTotalCount()
//        {
//            return _logs.Count;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("📝 InMemoryLogDatabase запущен");

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    // Периодическая очистка старых логов
//                    CleanupOldLogs();

//                    // Логируем статистику каждые 30 минут
//                    if (DateTime.UtcNow.Minute % 30 == 0 && DateTime.UtcNow.Second < 5)
//                    {
//                        _logger.LogInformation("📊 Статистика логов: {Count} записей (храним 24 часа)",
//                            _logs.Count);
//                    }

//                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
//                }
//                catch (OperationCanceledException)
//                {
//                    break;
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Ошибка в основном цикле InMemoryLogDatabase");
//                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
//                }
//            }

//            _logger.LogInformation("📝 InMemoryLogDatabase остановлен");
//        }
//    }
//}
