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

        // Подписываемся на события котировок
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

        _logger.LogInformation("✅ Подписался на quote:generated");

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
    //private void PrintQuote(QuoteGeneratedEvent quote)
    //{
    //    lock (_consoleLock)
    //    {
    //        var color = GetSymbolColor(quote.Symbol);
    //        // var timestamp = quote.Timestamp.ToString("HH:mm:ss");
    //        var timestamp = DateTime.Now.ToString("HH:mm:ss");
    //        var change = GetChangeIndicator(quote);
    //        var percentChange = GetPercentChange(quote);

    //        Console.ForegroundColor = color;
    //        Console.Write($"[{timestamp}] {quote.Symbol} ");

    //        // Цвет для направления
    //        if (quote.Close > quote.Open)
    //        {
    //            Console.ForegroundColor = ConsoleColor.Green;
    //            Console.Write("↑");
    //        }
    //        else if (quote.Close < quote.Open)
    //        {
    //            Console.ForegroundColor = ConsoleColor.Red;
    //            Console.Write("↓");
    //        }
    //        else
    //        {
    //            Console.ForegroundColor = ConsoleColor.Gray;
    //            Console.Write("→");
    //        }

    //        Console.ForegroundColor = color;
    //        Console.Write($" {quote.Close,8:F2}");

    //        // Процентное изменение
    //        if (quote.Close > quote.Open)
    //        {
    //            Console.ForegroundColor = ConsoleColor.Green;
    //            Console.Write($" (+{percentChange:F2}%)");
    //        }
    //        else if (quote.Close < quote.Open)
    //        {
    //            Console.ForegroundColor = ConsoleColor.Red;
    //            Console.Write($" ({percentChange:F2}%)");
    //        }
    //        else
    //        {
    //            Console.ForegroundColor = ConsoleColor.Gray;
    //            Console.Write($" (0.00%)");
    //        }

    //        Console.WriteLine();
    //        Console.ResetColor();
    //    }
    //}

    private ConsoleColor GetSymbolColor(string symbol)
    {
        return symbol switch
        {
            "AAA" => ConsoleColor.Cyan,
            "BBB" => ConsoleColor.Magenta,
            "CCC" => ConsoleColor.Yellow,
            "DDD" => ConsoleColor.Blue,
            "EEE" => ConsoleColor.Green,
            _ => ConsoleColor.White
        };
    }

    private string GetChangeIndicator(QuoteGeneratedEvent quote)
    {
        if (quote.Close > quote.Open) return "↑";
        if (quote.Close < quote.Open) return "↓";
        return "→";
    }

    private decimal GetPercentChange(QuoteGeneratedEvent quote)
    {
        if (quote.Open == 0) return 0;
        return ((quote.Close - quote.Open) / quote.Open) * 100;
    }

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


// Services/QuotesConsoleService.cs
// Services/QuotesConsoleService.cs (упрощенная работающая версия)

//using BusLibrary02.Core;
//using TradingPlatform.Events;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;

//namespace TradingPlatform.Services;

//public class QuotesConsoleService : BackgroundService
//{
//    private readonly ILogger<QuotesConsoleService> _logger;
//    private readonly IDynamicSubscriptionManager _subscriptionManager;
//    private readonly IEventHub _eventHub;
//    private IDisposable? _subscription;
//    private int _totalQuotes = 0;

//    public QuotesConsoleService(
//        ILogger<QuotesConsoleService> logger,
//        IDynamicSubscriptionManager subscriptionManager,
//        IEventHub eventHub)
//    {
//        _logger = logger;
//        _subscriptionManager = subscriptionManager;
//        _eventHub = eventHub;
//    }

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        _logger.LogInformation("🚀 QuotesConsoleService запущен");

//        // Регистрируем статический ключ для событий котировок (как в работающем проекте)
//        _subscriptionManager.RegisterStaticKey<QuoteGeneratedEvent>("quote:generated");

//        // Подписываемся на события котировок (как в работающем проекте)
//        _subscription = _subscriptionManager.Subscribe<QuoteGeneratedEvent>(
//            async (quote, ct) =>
//            {
//                try
//                {
//                    _totalQuotes++;

//                    // Просто выводим в консоль (как в работающем проекте)
//                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {quote.Symbol} → {quote.Close:F2}");

//                    // Периодически выводим статистику
//                    if (_totalQuotes % 10 == 0)
//                    {
//                        _logger.LogInformation($"📊 Обработано {_totalQuotes} котировок");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Ошибка обработки котировки для {Symbol}", quote.Symbol);
//                }
//            });

//        _logger.LogInformation("✅ Подписался на quote:generated");

//        try
//        {
//            // Ждем отмены (как в работающем проекте)
//            await Task.Delay(Timeout.Infinite, stoppingToken);
//        }
//        catch (OperationCanceledException)
//        {
//            _logger.LogDebug("QuotesConsoleService остановлен по запросу");
//        }
//        finally
//        {
//            _subscription?.Dispose();
//            _logger.LogInformation("🛑 QuotesConsoleService остановлен. Всего котировок: {Count}", _totalQuotes);
//        }
//    }
//}

// -----------
// Services/QuotesConsoleService.cs
//using BusLibrary02.Core;
//using TradingPlatform.Events;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;

//namespace TradingPlatform.Services;

//public class QuotesConsoleService : BackgroundService
//{
//    private readonly ILogger<QuotesConsoleService> _logger;
//    private readonly IDynamicSubscriptionManager _subscriptionManager;
//    private readonly IEventHub _eventHub;
//    private IDisposable? _subscription;
//    private int _quoteCount = 0;
//    private readonly Dictionary<string, ConsoleColor> _symbolColors;

//    public QuotesConsoleService(
//        ILogger<QuotesConsoleService> logger,
//        IDynamicSubscriptionManager subscriptionManager,
//        IEventHub eventHub)
//    {
//        _logger = logger;
//        _subscriptionManager = subscriptionManager;
//        _eventHub = eventHub;

//        // Инициализируем цвета для символов
//        _symbolColors = new Dictionary<string, ConsoleColor>
//        {
//            { "AAA", ConsoleColor.Green },
//            { "BBB", ConsoleColor.Blue },
//            { "CCC", ConsoleColor.Cyan },
//            { "DDD", ConsoleColor.Yellow },
//            { "EEE", ConsoleColor.Magenta },
//            { "FFF", ConsoleColor.Red },
//            { "GGG", ConsoleColor.DarkGreen },
//            { "HHH", ConsoleColor.DarkBlue },
//            { "III", ConsoleColor.DarkCyan },
//            { "JJJ", ConsoleColor.DarkYellow },
//            { "KKK", ConsoleColor.DarkMagenta },
//            { "LLL", ConsoleColor.DarkRed },
//            { "MMM", ConsoleColor.Gray },
//            { "NNN", ConsoleColor.DarkGray },
//            { "OOO", ConsoleColor.White },
//            { "PPP", ConsoleColor.Green },
//            { "QQQ", ConsoleColor.Blue },
//            { "RRR", ConsoleColor.Cyan },
//            { "SSS", ConsoleColor.Yellow },
//            { "TTT", ConsoleColor.Magenta },
//            { "UUU", ConsoleColor.Red },
//            { "VVV", ConsoleColor.DarkGreen },
//            { "WWW", ConsoleColor.DarkBlue },
//            { "XXX", ConsoleColor.DarkCyan }
//        };
//    }

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        _logger.LogInformation("🚀 QuotesConsoleService запущен");

//        // Регистрируем статический ключ для событий котировок
//        _subscriptionManager.RegisterStaticKey<QuoteGeneratedEvent>("quote:generated");

//        // Подписываемся на события котировок
//        _subscription = _subscriptionManager.Subscribe<QuoteGeneratedEvent>(
//            async (quote, ct) =>
//            {
//                try
//                {
//                    _quoteCount++;

//                    // Выводим котировку в консоль
//                    PrintQuoteToConsole(quote);

//                    // Периодически выводим статистику
//                    if (_quoteCount % 10 == 0)
//                    {
//                        PrintSummary();
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
//            // Ждем отмены
//            await Task.Delay(Timeout.Infinite, stoppingToken);
//        }
//        catch (OperationCanceledException)
//        {
//            _logger.LogDebug("QuotesConsoleService остановлен по запросу");
//        }
//        finally
//        {
//            _subscription?.Dispose();
//            PrintFinalStats();
//            _logger.LogInformation("🛑 QuotesConsoleService остановлен. Всего котировок: {Count}", _quoteCount);
//        }
//    }

//    private void PrintQuoteToConsole(QuoteGeneratedEvent quote)
//    {
//        // Получаем цвет для символа или используем белый по умолчанию
//        var color = _symbolColors.TryGetValue(quote.Symbol, out var symbolColor)
//            ? symbolColor
//            : ConsoleColor.White;

//        var timestamp = quote.Timestamp.ToString("HH:mm:ss");
//        var changeIndicator = GetChangeIndicator(quote);
//        var priceChange = GetPriceChangeIndicator(quote);

//        Console.ForegroundColor = color;
//        Console.WriteLine($"[{timestamp}] {quote.Symbol,-6} {changeIndicator} {priceChange} {quote.Close,8:F2} " +
//                         $"(O:{quote.Open,6:F2} H:{quote.High,6:F2} L:{quote.Low,6:F2}) " +
//                         $"V:{FormatVolume(quote.Volume),10}");
//        Console.ResetColor();
//    }

//    private string GetChangeIndicator(QuoteGeneratedEvent quote)
//    {
//        if (quote.Close > quote.Open) return "↑";
//        if (quote.Close < quote.Open) return "↓";
//        return "→";
//    }

//    private string GetPriceChangeIndicator(QuoteGeneratedEvent quote)
//    {
//        var percentChange = ((quote.Close - quote.Open) / quote.Open) * 100;
//        return percentChange >= 0
//            ? $"+{percentChange:F2}%"
//            : $"{percentChange:F2}%";
//    }

//    private void PrintWelcomeMessage()
//    {
//        Console.ForegroundColor = ConsoleColor.Magenta;
//        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
//        Console.WriteLine("║       МОНИТОРИНГ КОТИРОВОК В РЕАЛЬНОМ ВРЕМЕНИ      ║");
//        Console.WriteLine("╠═══════════════════════════════════════════════════╣");
//        Console.WriteLine("║ Формат: [Время] Символ Напр. Цена (O/H/L) Объем   ║");
//        Console.WriteLine("║ Цвета: Каждый тикер имеет свой уникальный цвет    ║");
//        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");
//        Console.ResetColor();
//    }

//    private void PrintSummary()
//    {
//        Console.ForegroundColor = ConsoleColor.Yellow;
//        Console.WriteLine($"\n📊 Обработано котировок: {_quoteCount} ({DateTime.Now:HH:mm:ss})");
//        Console.ResetColor();
//    }

//    private void PrintFinalStats()
//    {
//        Console.ForegroundColor = ConsoleColor.Cyan;
//        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
//        Console.WriteLine("║                ФИНАЛЬНАЯ СТАТИСТИКА               ║");
//        Console.WriteLine("╠═══════════════════════════════════════════════════╣");
//        Console.WriteLine($"║  Всего обработано: {_quoteCount,6} котировок             ║");
//        Console.WriteLine("╚═══════════════════════════════════════════════════╝\n");
//        Console.ResetColor();
//    }

//    private string FormatVolume(long volume)
//    {
//        decimal vol = volume;
//        return FormatVolume(vol);
//    }

//    private string FormatVolume(decimal volume)
//    {
//        if (volume >= 1_000_000_000)
//            return $"{(volume / 1_000_000_000.0m):F2}B";
//        if (volume >= 1_000_000)
//            return $"{(volume / 1_000_000.0m):F2}M";
//        if (volume >= 1_000)
//            return $"{(volume / 1_000.0m):F2}K";
//        return volume.ToString("N0");
//    }

//    public override async Task StopAsync(CancellationToken cancellationToken)
//    {
//        _logger.LogInformation("Остановка QuotesConsoleService...");
//        await base.StopAsync(cancellationToken);
//    }
//}