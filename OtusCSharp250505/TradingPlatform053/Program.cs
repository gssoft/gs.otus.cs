using BusLibrary02.Core;
using GS.Trade.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Reflection;
using TradingPlatform.Charts;
using TradingPlatform.Config;
using TradingPlatform.Events;
using TradingPlatform.Hubs;
using TradingPlatform.Services;

object consoleLock = new object();

var builder = WebApplication.CreateBuilder(args);

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ========== НАСТРОЙКА ЛОГГИНГА ==========
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "[HH:mm:ss] ";
});

// Уровни логирования по умолчанию
builder.Logging.SetMinimumLevel(LogLevel.Warning); // Показываем только Warning и выше

// Исключения для конкретных категорий:
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Information);
builder.Logging.AddFilter("System", LogLevel.Warning);

// Для наших сервисов настраиваем нужные уровни:
builder.Logging.AddFilter("TradingPlatform.Services.QuotesConsoleService", LogLevel.Information); // Котировки
builder.Logging.AddFilter("TradingPlatform.Services.StrategiesExecutionService", LogLevel.Warning); // Стратегии
builder.Logging.AddFilter("TradingPlatform.Services.TradingMonitorService", LogLevel.Warning); // Монитор
// builder.Logging.AddFilter("TradingPlatform.Charts.TickerChartAdapter", LogLevel.Debug);
// builder.Logging.AddFilter("TradingPlatform.Charts.ChartContainer", LogLevel.Debug); // Добавлено и Отбавлено обратно ХеХе

// EventHubStrategy - только важные сообщения (покупки/продажи)
builder.Logging.AddFilter("TradingPlatform.Services.EventHubStrategy", LogLevel.Information);

// InMemoryTradingDatabase - только ошибки (чтобы не загромождать)
builder.Logging.AddFilter("TradingPlatform.Services.InMemoryTradingDatabase", LogLevel.Warning);

// BusLibrary02 - только ошибки
builder.Logging.AddFilter("BusLibrary02", LogLevel.Warning);

// ========== КОНФИГУРАЦИЯ СЕРВИСОВ ==========

// 1. Сначала регистрируем EventHub
builder.Services.AddEventHub(options =>
{
    options.ChannelCapacity = 8192;
    options.Assemblies = new[] { typeof(Program).Assembly };
});

// 2. Регистрируем фабрику торговых объектов
builder.Services.AddSingleton<ITradingFactory, TradingFactory>();

// 3. Регистрируем менеджера тикеров
builder.Services.AddSingleton<EventHubTickerManager>();

// 4. Регистрируем ChartContainer КАК ФАБРИКУ, чтобы контролировать время создания
builder.Services.AddSingleton<ChartContainer>(provider =>
{
    var tickerManager = provider.GetRequiredService<EventHubTickerManager>();
    var eventHub = provider.GetRequiredService<IEventHub>();
    var subscriptionManager = provider.GetRequiredService<IDynamicSubscriptionManager>();
    var logger = provider.GetRequiredService<ILogger<ChartContainer>>();
    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

    logger.LogInformation("🚀 Создаем ChartContainer...");

    // Регистрируем статические ключи ДО создания ChartContainer
    logger.LogInformation("🔑 Регистрация статических ключей для ChartContainer...");

    subscriptionManager.RegisterStaticKey<TradingPlatform.Events.TradeExecutedEvent>("trade:executed");
    subscriptionManager.RegisterStaticKey<TradingPlatform.Events.ChartUpdateEvent>("chart:update");
    subscriptionManager.RegisterStaticKey<TradingPlatform.Events.QuoteGeneratedEvent>("quote:generated");
    subscriptionManager.RegisterStaticKey<TradingPlatform.Events.DealClosedEvent>("deal:closed");
    subscriptionManager.RegisterStaticKey<TradingPlatform.Events.OrderCreatedEvent>("order:created");
    subscriptionManager.RegisterStaticKey<TradingPlatform.Events.PositionChangedEvent>("position:changed");
    subscriptionManager.RegisterStaticKey<TradingPlatform.Events.SystemStatusEvent>("system:status");

    logger.LogInformation("✅ Статические ключи зарегистрированы");

    // Теперь создаем ChartContainer
    var chartContainer = new ChartContainer(
        tickerManager,
        eventHub,
        subscriptionManager,
        logger,
        loggerFactory
    );

    logger.LogInformation("✅ ChartContainer создан");

    return chartContainer;
});

// 5. Регистрируем InMemoryTradingDatabase ДО фоновых сервисов
builder.Services.AddSingleton<InMemoryTradingDatabase>();
builder.Services.AddSingleton<IInMemoryTradingDatabase>(provider =>
    provider.GetRequiredService<InMemoryTradingDatabase>());

// 6. SignalR ДО фоновых сервисов
builder.Services.AddSignalR();

// 7. Регистрируем фоновые сервисы в правильном порядке
builder.Services.AddHostedService<QuotesGeneratorService>();
builder.Services.AddHostedService<StrategiesExecutionService>();
builder.Services.AddHostedService<QuotesConsoleService>();
builder.Services.AddHostedService<TradingMonitorService>();

// InMemoryTradingDatabase должен быть ПОСЛЕ стратегий, но ДО Monitor
builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<InMemoryTradingDatabase>());

// 8. Регистрация обработчиков событий
builder.Services.AddSingleton<TradingPlatform.Handlers.SystemEventHandler>();

// 9. Web компоненты
builder.Services.AddRazorPages();
builder.Services.AddControllers();

var app = builder.Build();

// ========== ДОПОЛНИТЕЛЬНАЯ РЕГИСТРАЦИЯ СТАТИЧЕСКИХ КЛЮЧЕЙ ==========
try
{
    using var scope = app.Services.CreateScope();
    var subscriptionManager = scope.ServiceProvider.GetRequiredService<IDynamicSubscriptionManager>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("🔑 Дополнительная регистрация статических ключей EventHub...");

    // Находим все типы сообщений с атрибутом MessageKey
    var messageTypes = typeof(Program).Assembly.GetTypes()
        .Where(t => typeof(IMessage).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
        .Where(t => t.GetCustomAttribute<MessageKeyAttribute>() != null);

    int registeredCount = 0;
    foreach (var type in messageTypes)
    {
        try
        {
            var attr = type.GetCustomAttribute<MessageKeyAttribute>();
            if (attr != null)
            {
                // Используем рефлексию для вызова RegisterStaticKey<T>
                var method = typeof(DynamicSubscriptionManager).GetMethod("RegisterStaticKey");
                var genericMethod = method!.MakeGenericMethod(type);
                genericMethod.Invoke(subscriptionManager, new object[] { attr.Key });

                logger.LogDebug("Зарегистрирован ключ '{Key}' для {Type}", attr.Key, type.Name);
                registeredCount++;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка регистрации ключа для {Type}", type.Name);
        }
    }

    logger.LogInformation("✅ Всего зарегистрировано {Count} статических ключей", registeredCount);

    // Проверяем, какие ключи зарегистрированы
    var registeredKeys = subscriptionManager.GetSubscribedKeys().ToList();
    logger.LogInformation("📋 Зарегистрированные ключи:");
    foreach (var key in registeredKeys)
    {
        logger.LogInformation("  • {Key}", key);
    }
}
catch (Exception ex)
{
    var globalLogger = app.Services.GetRequiredService<ILogger<Program>>();
    globalLogger.LogError(ex, "❌ Ошибка при регистрации статических ключей");
}

// ========== КОНФИГУРАЦИЯ ПРИЛОЖЕНИЯ ==========

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Важно: UseRouting должен быть перед SignalR
app.UseRouting();

// SignalR hub должен быть замаплен ДО UseEndpoints
app.MapHub<TradingDataHub>("/tradingDataHub");

app.UseAuthorization();

// API endpoints
app.MapGet("/api/trading/summaries", (IInMemoryTradingDatabase database) =>
{
    return Results.Ok(database.GetSummaries());
});

app.MapGet("/api/trading/trades", (IInMemoryTradingDatabase database,
    [FromQuery] string? ticker,
    [FromQuery] string? strategy,
    [FromQuery] int page = 1) =>
{
    return Results.Ok(database.GetTrades(ticker, strategy, page));
});

app.MapGet("/api/trading/deals", (IInMemoryTradingDatabase database,
    [FromQuery] string? ticker,
    [FromQuery] string? strategy,
    [FromQuery] int page = 1) =>
{
    return Results.Ok(database.GetDeals(ticker, strategy, page));
});

app.MapGet("/api/trading/orders", (IInMemoryTradingDatabase database,
    [FromQuery] string? ticker,
    [FromQuery] string? strategy,
    [FromQuery] int page = 1) =>
{
    return Results.Ok(database.GetOrders(ticker, strategy, page));
});

// API для проверки EventHub
app.MapGet("/api/eventhub/status", (IEventHub eventHub, IDynamicSubscriptionManager subscriptionManager) =>
{
    var subscriptions = subscriptionManager.GetSubscribedKeys().ToList();
    return Results.Ok(new
    {
        Status = "Active",
        SubscriptionsCount = subscriptions.Count,
        Subscriptions = subscriptions,
        Timestamp = DateTime.Now
    });
});

// API для проверки стратегий
app.MapGet("/api/strategies/status", (EventHubTickerManager tickerManager) =>
{
    try
    {
        var tickers = tickerManager.GetAllTickers();
        var strategies = tickers.SelectMany(t => t.Strategies).ToList();
        int runningCount = 0;
        foreach (var strategy in strategies)
        {
            if (strategy is EventHubStrategy eventHubStrategy && eventHubStrategy.IsRunning())
            {
                runningCount++;
            }
        }

        return Results.Ok(new
        {
            TotalTickers = tickers.Count,
            TotalStrategies = strategies.Count,
            RunningStrategies = runningCount
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error getting strategies status: {ex.Message}");
    }
});

// API для проверки InMemoryDatabase
app.MapGet("/api/trading/status", (IInMemoryTradingDatabase database) =>
{
    var summaries = database.GetSummaries();
    var trades = database.GetTrades(null, null, 1, 5);
    var deals = database.GetDeals(null, null, 1, 5);
    var orders = database.GetOrders(null, null, 1, 5);

    return Results.Ok(new
    {
        DatabaseStatus = "Active",
        SummariesCount = summaries.Count(),
        TradesCount = trades.TotalCount,
        DealsCount = deals.TotalCount,
        OrdersCount = orders.TotalCount,
        Timestamp = DateTime.Now
    });
});

// API для проверки событий сделок
app.MapGet("/api/debug/trade-events", (IEventHub eventHub, EventHubTickerManager tickerManager) =>
{
    var tickers = tickerManager.GetAllTickers();
    var symbols = tickers.Select(t => t.Symbol).ToList();

    return Results.Ok(new
    {
        Timestamp = DateTime.UtcNow,
        Symbols = symbols,
        Message = "Отправьте POST запрос с данными сделки на /api/debug/test-trade",
        TestEndpoint = "/api/debug/test-trade",
        TestBody = new
        {
            Symbol = "AAA",
            Side = "Buy",
            Price = 1000.50m,
            Quantity = 100,
            StrategyName = "TestStrategy",
            Timestamp = DateTime.UtcNow
        }
    });
});

// API для тестирования сделок
//app.MapPost("/api/debug/test-trade", (HttpContext context, ChartContainer chartContainer) =>
//{
//    try
//    {
//        var reader = new StreamReader(context.Request.Body);
//        var body = reader.ReadToEndAsync().GetAwaiter().GetResult();

//        // Парсим JSON
//        var tradeEvent = System.Text.Json.JsonSerializer.Deserialize<TradeExecutedEvent>(
//            body,
//            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
//        );

//        if (tradeEvent == null)
//        {
//            return Results.BadRequest("Invalid trade data");
//        }

//        // Находим соответствующий адаптер
//        var adapter = chartContainer.ChartAdapters.FirstOrDefault(a => a.TickerSymbol == tradeEvent.Symbol);
//        if (adapter != null)
//        {
//            adapter.AddTrade(tradeEvent);
//            return Results.Ok(new
//            {
//                Success = true,
//                Message = $"Trade added for {tradeEvent.Symbol}",
//                Trade = tradeEvent
//            });
//        }
//        else
//        {
//            return Results.NotFound(new
//            {
//                Success = false,
//                Message = $"No chart adapter found for symbol {tradeEvent.Symbol}",
//                AvailableSymbols = chartContainer.ChartAdapters.Select(a => a.TickerSymbol).ToList()
//            });
//        }
//    }
//    catch (Exception ex)
//    {
//        return Results.Problem($"Error: {ex.Message}");
//    }
//});

app.MapGet("/api/trading/diagnostic", (
    IInMemoryTradingDatabase database,
    IEventHub eventHub,
    IDynamicSubscriptionManager subscriptionManager,
    EventHubTickerManager tickerManager) =>
{
    var summaries = database.GetSummaries();
    var tickers = tickerManager.GetAllTickers();

    return Results.Ok(new
    {
        Timestamp = DateTime.UtcNow,
        InMemoryDatabase = new
        {
            Status = "Active",
            SummariesCount = summaries.Count(),
            TickersWithData = summaries.Select(s => s.Ticker).Distinct().Count(),
            StrategiesWithData = summaries.Select(s => s.Strategy).Distinct().Count()
        },
        TickerManager = new
        {
            TickersCount = tickers.Count,
            TotalStrategies = tickers.Sum(t => t.Strategies.Count)
        },
        EventHub = new
        {
            SubscriptionsCount = subscriptionManager.GetSubscribedKeys().Count()
        }
    });
});

app.MapGet("/api/debug/deals", (IInMemoryTradingDatabase database) =>
{
    var allDeals = database.GetDeals(null, null, 1, 100);

    return Results.Ok(new
    {
        TotalDeals = allDeals.TotalCount,
        Deals = allDeals.Items.Select(d => new
        {
            d.Ticker,
            d.Strategy,
            d.PnL,
            d.Timestamp,
            d.Side,
            d.OpenPrice,
            d.ClosePrice
        }),
        GroupedByStrategy = allDeals.Items
            .GroupBy(d => d.Strategy)
            .Select(g => new
            {
                Strategy = g.Key,
                Count = g.Count(),
                TotalPnL = g.Sum(d => d.PnL)
            })
    });
});

// 25.12.31 -----------------
// API для получения информации о портах
app.MapGet("/api/ports", () =>
{
    var server = app.Services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
    var addresses = server?.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses
        ?? new List<string> { "http://localhost:5000" };

    return Results.Ok(new
    {
        Status = "Running",
        Timestamp = DateTime.Now,
        Application = "Торговая платформа",
        Addresses = addresses,
        MainUrl = addresses.FirstOrDefault() ?? "http://localhost:5000",
        Pages = new[]
        {
            new { Name = "Графики", Path = "/Finance" },
            new { Name = "Сводка", Path = "/Summary" },
            new { Name = "Сделки", Path = "/Trades" },
            new { Name = "Ордера", Path = "/Orders" },
            new { Name = "Сделки", Path = "/Deals" },
            new { Name = "Позиции", Path = "/Positions" }
        }
        .Select(p => new
        {
            p.Name,
            p.Path,
            FullUrl = $"{addresses.FirstOrDefault() ?? "http://localhost:5000"}{p.Path}"
        })
    });
});
// --------------------------

app.MapRazorPages();
app.MapControllers();

// ----------------------------------------------------------------
// 25.12.31 4
// Простой startup
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var configuration = app.Services.GetRequiredService<IConfiguration>();

    // Получаем конфигурацию из appsettings.json
    var fileConfig = configuration.GetSection("FileOutput").Get<FileOutputConfig>() ?? new FileOutputConfig();
    var displayConfig = configuration.GetSection("Display").Get<DisplayConfig>() ?? new DisplayConfig();
    var appInfoConfig = configuration.GetSection("ApplicationInfo").Get<ApplicationInfoConfig>() ?? new ApplicationInfoConfig();

    logger.LogInformation("🚀 {AppName} v{Version} ЗАПУЩЕНА", appInfoConfig.Name, appInfoConfig.Version);

    // Получаем информацию о портах из сервера
    var server = app.Services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
    var addresses = server?.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses;

    // Определяем основной адрес для генерации ссылок
    string mainAddress = "http://localhost:5000"; // по умолчанию
    if (addresses != null && addresses.Any())
    {
        mainAddress = addresses.First();
    }

    Console.WriteLine("\n" + new string('=', 80));
    Console.WriteLine("🚀 {0} v{1} УСПЕШНО ЗАПУЩЕНА!", appInfoConfig.Name, appInfoConfig.Version);
    Console.WriteLine(new string('=', 80));

    // ========== ЗАПИСЬ В ФАЙЛ ИЗ КОНФИГУРАЦИИ ==========
    if (fileConfig.EnableFileOutput)
    {
        try
        {
            // Создаем директорию для файлов, если она указана
            string outputDir = fileConfig.OutputDirectory;
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Формируем полные пути к файлам
            string portsFilePath = string.IsNullOrEmpty(outputDir)
                ? fileConfig.PortsFileName
                : Path.Combine(outputDir, fileConfig.PortsFileName);

            string statsFilePath = string.IsNullOrEmpty(outputDir)
                ? fileConfig.StatsFileName
                : Path.Combine(outputDir, fileConfig.StatsFileName);

            // Записываем файл с URL
            var fileContent = new System.Text.StringBuilder();
            fileContent.AppendLine("=".PadRight(80, '='));
            fileContent.AppendLine($"🚀 {appInfoConfig.Name} v{appInfoConfig.Version} - ИНФОРМАЦИЯ ДЛЯ ПОДКЛЮЧЕНИЯ");
            fileContent.AppendLine("=".PadRight(80, '='));
            fileContent.AppendLine($"📅 Время запуска: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            fileContent.AppendLine();

            if (addresses != null && addresses.Any())
            {
                fileContent.AppendLine("🌐 Приложение доступно по следующим адресам:");
                foreach (var address in addresses)
                {
                    fileContent.AppendLine($"   🔗 {address}");
                }
                fileContent.AppendLine();

                // Записываем основные страницы
                fileContent.AppendLine("📋 ОСНОВНЫЕ СТРАНИЦЫ:");

                var pages = new[]
                {
                    new { Name = "Графики", Path = "/Finance", Icon = "📊" },
                    new { Name = "Сводка", Path = "/Summary", Icon = "📈" },
                    new { Name = "Сделки", Path = "/Trades", Icon = "💰" },
                    new { Name = "Ордера", Path = "/Orders", Icon = "📝" },
                    new { Name = "Сделки", Path = "/Deals", Icon = "🤝" },
                    new { Name = "Позиции", Path = "/Positions", Icon = "📊" }
                };

                foreach (var page in pages)
                {
                    fileContent.AppendLine($"   {page.Icon} {page.Name,-10} - {mainAddress}{page.Path}");
                }
                fileContent.AppendLine();

                // API endpoints (если включены в конфигурации)
                if (displayConfig.ShowApiEndpoints)
                {
                    fileContent.AppendLine("🛠️ API ENDPOINTS:");
                    var apiEndpoints = new[]
                    {
                        new { Name = "Статус системы", Path = "/api/trading/status" },
                        new { Name = "Статус стратегий", Path = "/api/strategies/status" },
                        new { Name = "Статус EventHub", Path = "/api/eventhub/status" },
                        new { Name = "Информация о портах", Path = "/api/ports" },
                        new { Name = "Все сводки", Path = "/api/trading/summaries" },
                        new { Name = "Сделки (API)", Path = "/api/trading/trades" },
                        new { Name = "Ордера (API)", Path = "/api/trading/orders" },
                        new { Name = "Закрытые сделки (API)", Path = "/api/trading/deals" },
                        new { Name = "Тест сделок", Path = "/api/debug/test-trade" } //, Icon = "🧪" }
                    };

                    //var apiEndpoints = new[]
                    //{
                    //    new { Name = "Статус системы", Path = "/api/trading/status" },
                    //    new { Name = "Статус стратегий", Path = "/api/strategies/status" },
                    //    new { Name = "Статус EventHub", Path = "/api/eventhub/status" },
                    //    new { Name = "Информация о портах", Path = "/api/ports" },
                    //    new { Name = "Все сводки", Path = "/api/trading/summaries" },
                    //    new { Name = "Сделки (API)", Path = "/api/trading/trades" },
                    //    new { Name = "Ордера (API)", Path = "/api/trading/orders" },
                    //    new { Name = "Закрытые сделки (API)", Path = "/api/trading/deals" },

                    //};

                    foreach (var endpoint in apiEndpoints)
                    {
                        fileContent.AppendLine($"   🔌 {endpoint.Name,-25} - {mainAddress}{endpoint.Path}");
                    }
                    fileContent.AppendLine();
                }

                fileContent.AppendLine("⚡ ДЛЯ БЫСТРОГО СТАРТА:");
                fileContent.AppendLine($"   Откройте в браузере: {mainAddress}/Finance");
                fileContent.AppendLine();

                fileContent.AppendLine("📡 SIGNALR HUB:");
                fileContent.AppendLine($"   Подключение: {mainAddress}/tradingDataHub");
            }
            else
            {
                fileContent.AppendLine("⚠️ Не удалось определить адреса приложения.");
                fileContent.AppendLine("   Используйте стандартные адреса:");
                fileContent.AppendLine("   🔗 http://localhost:5000");
                fileContent.AppendLine("   🔗 https://localhost:5001");
            }

            fileContent.AppendLine();
            fileContent.AppendLine("=".PadRight(80, '='));
            fileContent.AppendLine($"Файл создан: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            fileContent.AppendLine($"Конфигурация: portsFileName={fileConfig.PortsFileName}, outputDir={fileConfig.OutputDirectory}");

            File.WriteAllText(portsFilePath, fileContent.ToString(), System.Text.Encoding.UTF8);

            Console.WriteLine($"📄 Информация о подключении сохранена в файл:");
            Console.WriteLine($"   📁 {Path.GetFullPath(portsFilePath)}");
            Console.WriteLine($"   🔗 file://{Path.GetFullPath(portsFilePath).Replace("\\", "/")}");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Не удалось записать информацию в файл");
            Console.WriteLine($"⚠️ Не удалось записать информацию в файл: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("ℹ️  Запись в файл отключена в конфигурации (FileOutput.EnableFileOutput = false)");
    }
    // ========== КОНЕЦ ЗАПИСИ В ФАЙЛ ==========

    // Вывод в консоль
    if (addresses != null && addresses.Any())
    {
        Console.WriteLine("🌐 Приложение доступно по следующим адресам:");
        foreach (var address in addresses)
        {
            Console.WriteLine($"   🔗 {address}");
        }

        if (displayConfig.ShowAllPages)
        {
            Console.WriteLine($"\n📋 Основные страницы:");
            var pages = new[]
            {
                new { Name = "Графики", Path = "/Finance", Icon = "📊" },
                new { Name = "Сводка", Path = "/Summary", Icon = "📈" },
                new { Name = "Сделки", Path = "/Trades", Icon = "💰" },
                new { Name = "Ордера", Path = "/Orders", Icon = "📝" },
                new { Name = "Сделки", Path = "/Deals", Icon = "🤝" },
                new { Name = "Позиции", Path = "/Positions", Icon = "📊" }
            };

            foreach (var page in pages)
            {
                Console.WriteLine($"   {page.Icon} {page.Name,-10} - {mainAddress}{page.Path}");
            }
        }

        Console.WriteLine($"\n⚡ Для быстрого старта откройте в браузере:");
        Console.WriteLine($"   {mainAddress}/Finance");
    }
    else
    {
        Console.WriteLine("🌐 Приложение доступно по стандартным адресам:");
        Console.WriteLine("   🔗 http://localhost:5000");
        Console.WriteLine("   🔗 https://localhost:5001");
        Console.WriteLine("\n⚡ Для быстрого старта откройте в браузере:");
        Console.WriteLine("   http://localhost:5000/Finance");
    }

    Console.WriteLine($"\n⏳ Пауза {displayConfig.PauseSeconds} секунд для просмотра информации...");
    Console.WriteLine(new string('=', 80));

    // Пауза из конфигурации
    Thread.Sleep(displayConfig.PauseSeconds * 1000);

    // Получаем статистику системы
    var tickerManager = app.Services.GetRequiredService<EventHubTickerManager>();
    var tickers = tickerManager.GetAllTickers();
    var database = app.Services.GetRequiredService<IInMemoryTradingDatabase>();
    var summaries = database.GetSummaries();

    Console.WriteLine("\n" + new string('=', 60));
    Console.WriteLine("📊 СТАТИСТИКА СИСТЕМЫ");
    Console.WriteLine(new string('=', 60));
    Console.WriteLine($"✅ Тикеров: {tickers.Count}");
    Console.WriteLine($"✅ Стратегий: {tickers.Sum(t => t.Strategies.Count)}");
    Console.WriteLine($"✅ InMemoryDatabase: {summaries.Count()} записей");
    Console.WriteLine($"✅ SignalR Hub: {mainAddress}/tradingDataHub");
    Console.WriteLine(new string('=', 60) + "\n");

    // Дополнительно: записать статистику в файл (если включено)
    if (fileConfig.EnableFileOutput)
    {
        try
        {
            string statsFilePath = string.IsNullOrEmpty(fileConfig.OutputDirectory)
                ? fileConfig.StatsFileName
                : Path.Combine(fileConfig.OutputDirectory, fileConfig.StatsFileName);

            var statsContent = $"СТАТИСТИКА СИСТЕМЫ - {appInfoConfig.Name} v{appInfoConfig.Version}\n" +
                              $"{new string('=', 60)}\n" +
                              $"Время: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                              $"Тикеров: {tickers.Count}\n" +
                              $"Стратегий: {tickers.Sum(t => t.Strategies.Count)}\n" +
                              $"Записей в базе: {summaries.Count()}\n" +
                              $"\n📈 ТИКЕРЫ И СТРАТЕГИИ:\n";

            // Группируем по тикерам
            var tickersBySymbol = tickers.GroupBy(t => t.Symbol)
                .OrderBy(g => g.Key);

            foreach (var group in tickersBySymbol)
            {
                var ticker = group.First();
                statsContent += $"\n  • {ticker.Symbol}:\n";
                foreach (var strategy in ticker.Strategies)
                {
                    if (strategy is EventHubStrategy eventHubStrategy)
                    {
                        statsContent += $"      ◦ {eventHubStrategy.StrategyName}\n";
                    }
                    else
                    {
                        statsContent += $"      ◦ {strategy.GetType().Name}\n";
                    }
                }
            }

            statsContent += $"\n📊 СВОДКА ПО СТРАТЕГИЯМ:\n";
            var summaryGroups = summaries
                .GroupBy(s => s.Strategy)
                .Select(g => new
                {
                    Strategy = g.Key,
                    Count = g.Count(),
                    TotalPnL = g.Sum(s => s.TotalPnL)
                })
                .OrderByDescending(x => x.TotalPnL);

            foreach (var group in summaryGroups)
            {
                statsContent += $"  • {group.Strategy}: {group.Count} записей, PnL: {group.TotalPnL:F2}\n";
            }

            File.WriteAllText(statsFilePath, statsContent, System.Text.Encoding.UTF8);

            Console.WriteLine($"📊 Статистика сохранена в файл: {Path.GetFullPath(statsFilePath)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Не удалось записать статистику в файл");
            Console.WriteLine($"⚠️ Не удалось записать статистику в файл: {ex.Message}");
        }
    }

    // Дополнительная информация для логов
    logger.LogInformation("Система инициализирована: {TickersCount} тикеров, {StrategiesCount} стратегий",
        tickers.Count, tickers.Sum(t => t.Strategies.Count));

    // Тестирование событий
    var chartContainer = app.Services.GetRequiredService<ChartContainer>();
    logger.LogInformation("ChartContainer создан с {Count} адаптерами", chartContainer.Count);
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("🛑 Остановка приложения...");

    try
    {
        var tickerManager = app.Services.GetRequiredService<EventHubTickerManager>();
        tickerManager.StopAllStrategies();
        logger.LogInformation("Все стратегии остановлены");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка при остановке стратегий");
    }
});

app.Run();



