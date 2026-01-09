// Services/QuotesConsoleService.cs
// Services/QuotesConsoleService.cs
using BusLibrary02.Core;
using TradingPlatform.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TradingPlatform.Services;

public class QuotesConsoleService : BackgroundService
{
    private readonly ILogger<QuotesConsoleService> _logger;
    private readonly IDynamicSubscriptionManager _subscriptionManager;
    private int _totalQuotes = 0;
    private DateTime _startTime = DateTime.Now;
    private readonly object _consoleLock = new();

    public QuotesConsoleService(
        ILogger<QuotesConsoleService> logger,
        IDynamicSubscriptionManager subscriptionManager)
    {
        _logger = logger;
        _subscriptionManager = subscriptionManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 QuotesConsoleService запущен");

        // Ждем инициализации других сервисов
        await Task.Delay(1000, stoppingToken);

        // Регистрируем статический ключ
        _subscriptionManager.RegisterStaticKey<QuoteGeneratedEvent>("quote:generated");

        // Подписываемся на события котировок ТОЛЬКО для вывода в консоль
        var subscription = _subscriptionManager.Subscribe<QuoteGeneratedEvent>(
            async (quote, ct) =>
            {
                try
                {
                    Interlocked.Increment(ref _totalQuotes);
                    PrintQuote(quote);

                    // Периодически выводим статистику
                    if (_totalQuotes % 10 == 0)
                    {
                        PrintStats();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки котировки для {Symbol}", quote.Symbol);
                }
            });

        _logger.LogInformation("✅ Подписался на quote:generated (только консольный вывод)");

        // Выводим заголовок
        PrintWelcomeMessage();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("QuotesConsoleService остановлен");
        }
        finally
        {
            subscription?.Dispose();
            PrintFinalStats();
            _logger.LogInformation("🛑 QuotesConsoleService остановлен. Всего котировок: {Count}", _totalQuotes);
        }
    }

    // ... остальные методы PrintQuote, PrintWelcomeMessage, PrintStats, PrintFinalStats без изменений
    private void PrintQuote(QuoteGeneratedEvent quote)
    {
        lock (_consoleLock)
        {
            var color = GetSymbolColor(quote.Symbol);
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var change = GetChangeIndicator(quote);
            var percentChange = GetPercentChange(quote);

            Console.ForegroundColor = color;
            Console.Write($"[{timestamp}] {quote.Symbol} ");

            // Цвет для направления
            if (quote.Close > quote.Open)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("↑");
            }
            else if (quote.Close < quote.Open)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("↓");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("→");
            }

            Console.ForegroundColor = color;
            Console.Write($" {quote.Close,8:F2}");

            // Процентное изменение
            if (quote.Close > quote.Open)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" (+{percentChange:F2}%)");
            }
            else if (quote.Close < quote.Open)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($" ({percentChange:F2}%)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($" (0.00%)");
            }

            Console.WriteLine();
            Console.ResetColor();
        }
    }





//using BusLibrary02.Core;
//using TradingPlatform.Events;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;

//namespace TradingPlatform.Services;

//public class QuotesConsoleService : BackgroundService
//{
//    private readonly ILogger<QuotesConsoleService> _logger;
//    private readonly IDynamicSubscriptionManager _subscriptionManager;
//    private int _totalQuotes = 0;
//    private DateTime _startTime = DateTime.Now;
//    private readonly object _consoleLock = new();

//    private readonly IInMemoryLogDatabase? _logDatabase;

//    //public QuotesConsoleService(
//    //    ILogger<QuotesConsoleService> logger,
//    //    IDynamicSubscriptionManager subscriptionManager)
//    //{
//    //    _logger = logger;
//    //    _subscriptionManager = subscriptionManager;
//    //}

//    public QuotesConsoleService(
//        ILogger<QuotesConsoleService> logger,
//        IDynamicSubscriptionManager subscriptionManager,
//        IInMemoryLogDatabase? logDatabase = null) // Добавляем параметр
//    {
//        _logger = logger;
//        _subscriptionManager = subscriptionManager;
//        _logDatabase = logDatabase;
//    }


//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        _logger.LogInformation("🚀 QuotesConsoleService запущен");

//        // Ждем инициализации других сервисов
//        await Task.Delay(1000, stoppingToken);

//        // Регистрируем статический ключ
//        _subscriptionManager.RegisterStaticKey<QuoteGeneratedEvent>("quote:generated");

//        // Подписываемся на события котировок
//        var subscription = _subscriptionManager.Subscribe<QuoteGeneratedEvent>(
//            async (quote, ct) =>
//            {
//                try
//                {
//                    Interlocked.Increment(ref _totalQuotes);
//                    PrintQuote(quote);

//                    // Периодически выводим статистику
//                    if (_totalQuotes % 10 == 0)
//                    {
//                        PrintStats();
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Ошибка обработки котировки для {Symbol}", quote.Symbol);
//                }
//            });

//        _logger.LogInformation("✅ Подписался на quote:generated");

//        // Выводим заголовок
//        PrintWelcomeMessage();

//        try
//        {
//            await Task.Delay(Timeout.Infinite, stoppingToken);
//        }
//        catch (OperationCanceledException)
//        {
//            _logger.LogDebug("QuotesConsoleService остановлен");
//        }
//        finally
//        {
//            subscription?.Dispose();
//            PrintFinalStats();
//            _logger.LogInformation("🛑 QuotesConsoleService остановлен. Всего котировок: {Count}", _totalQuotes);
//        }
//    }
//    //private void PrintQuote(QuoteGeneratedEvent quote)
//    //{
//    //    lock (_consoleLock)
//    //    {
//    //        var color = GetSymbolColor(quote.Symbol);
//    //        var timestamp = DateTime.Now.ToString("HH:mm:ss");
//    //        var change = GetChangeIndicator(quote);
//    //        var percentChange = GetPercentChange(quote);

//    //        Console.ForegroundColor = color;
//    //        Console.Write($"[{timestamp}] {quote.Symbol} ");

//    //        // Цвет для направления
//    //        if (quote.Close > quote.Open)
//    //        {
//    //            Console.ForegroundColor = ConsoleColor.Green;
//    //            Console.Write("↑");
//    //        }
//    //        else if (quote.Close < quote.Open)
//    //        {
//    //            Console.ForegroundColor = ConsoleColor.Red;
//    //            Console.Write("↓");
//    //        }
//    //        else
//    //        {
//    //            Console.ForegroundColor = ConsoleColor.Gray;
//    //            Console.Write("→");
//    //        }

//    //        Console.ForegroundColor = color;
//    //        Console.Write($" {quote.Close,8:F2}");

//    //        // Процентное изменение
//    //        if (quote.Close > quote.Open)
//    //        {
//    //            Console.ForegroundColor = ConsoleColor.Green;
//    //            Console.Write($" (+{percentChange:F2}%)");
//    //        }
//    //        else if (quote.Close < quote.Open)
//    //        {
//    //            Console.ForegroundColor = ConsoleColor.Red;
//    //            Console.Write($" ({percentChange:F2}%)");
//    //        }
//    //        else
//    //        {
//    //            Console.ForegroundColor = ConsoleColor.Gray;
//    //            Console.Write($" (0.00%)");
//    //        }

//    //        Console.WriteLine();
//    //        Console.ResetColor();
//    //    }
//    //}

//    private void PrintQuote(QuoteGeneratedEvent quote)
//    {
//        lock (_consoleLock)
//        {
//            var color = GetSymbolColor(quote.Symbol);
//            var timestamp = DateTime.Now.ToString("HH:mm:ss");
//            var change = GetChangeIndicator(quote);
//            var percentChange = GetPercentChange(quote);

//            Console.ForegroundColor = color;
//            Console.Write($"[{timestamp}] {quote.Symbol} ");

//            // Цвет для направления
//            if (quote.Close > quote.Open)
//            {
//                Console.ForegroundColor = ConsoleColor.Green;
//                Console.Write("↑");
//            }
//            else if (quote.Close < quote.Open)
//            {
//                Console.ForegroundColor = ConsoleColor.Red;
//                Console.Write("↓");
//            }
//            else
//            {
//                Console.ForegroundColor = ConsoleColor.Gray;
//                Console.Write("→");
//            }

//            Console.ForegroundColor = color;
//            Console.Write($" {quote.Close,8:F2}");

//            // Процентное изменение
//            string percentText;
//            if (quote.Close > quote.Open)
//            {
//                Console.ForegroundColor = ConsoleColor.Green;
//                percentText = $"(+{percentChange:F2}%)";
//                Console.Write($" (+{percentChange:F2}%)");
//            }
//            else if (quote.Close < quote.Open)
//            {
//                Console.ForegroundColor = ConsoleColor.Red;
//                percentText = $"({percentChange:F2}%)";
//                Console.Write($" ({percentChange:F2}%)");
//            }
//            else
//            {
//                Console.ForegroundColor = ConsoleColor.Gray;
//                percentText = "(0.00%)";
//                Console.Write($" (0.00%)");
//            }

//            Console.WriteLine();
//            Console.ResetColor();

//            // Записываем в базу логов
//            var logMessage = $"[{timestamp}] {quote.Symbol} {change} {quote.Close:F2} {percentText}";
//            _logDatabase?.AddLog(logMessage, quote.Symbol);
//        }
//    }


//    private ConsoleColor GetSymbolColor(string symbol)
//    {
//        return symbol switch
//        {
//            "AAA" => ConsoleColor.Cyan,
//            "BBB" => ConsoleColor.Magenta,
//            "CCC" => ConsoleColor.Yellow,
//            "DDD" => ConsoleColor.Blue,
//            "EEE" => ConsoleColor.Green,
//            _ => ConsoleColor.White
//        };
//    }

//    private string GetChangeIndicator(QuoteGeneratedEvent quote)
//    {
//        if (quote.Close > quote.Open) return "↑";
//        if (quote.Close < quote.Open) return "↓";
//        return "→";
//    }

//    private decimal GetPercentChange(QuoteGeneratedEvent quote)
//    {
//        if (quote.Open == 0) return 0;
//        return ((quote.Close - quote.Open) / quote.Open) * 100;
//    }

    private void PrintWelcomeMessage()
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
            Console.WriteLine("║        РЕАЛЬНОЕ ВРЕМЯ: МОНИТОРИНГ КОТИРОВОК       ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════╣");
            Console.WriteLine("║ [Время] Символ ↑ Цена (+Изменение %)             ║");
            Console.WriteLine("║ Цвета: AAA-голубой                              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");
            Console.ResetColor();
        }
    }

    private void PrintStats()
    {
        lock (_consoleLock)
        {
            var elapsed = DateTime.Now - _startTime;
            var rate = elapsed.TotalSeconds > 0 ? _totalQuotes / elapsed.TotalSeconds : 0;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n📊 Статистика: {_totalQuotes} котировок за {elapsed:mm\\:ss} ({rate:F1}/сек)");
            Console.ResetColor();
        }
    }

    private void PrintFinalStats()
    {
        lock (_consoleLock)
        {
            var elapsed = DateTime.Now - _startTime;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("ФИНАЛЬНАЯ СТАТИСТИКА");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"Время работы: {elapsed:hh\\:mm\\:ss}");
            Console.WriteLine($"Обработано котировок: {_totalQuotes}");
            Console.WriteLine($"Средняя скорость: {_totalQuotes / Math.Max(elapsed.TotalSeconds, 1):F1} котир./сек");
            Console.WriteLine(new string('=', 50));
            Console.ResetColor();
        }
    }
}


