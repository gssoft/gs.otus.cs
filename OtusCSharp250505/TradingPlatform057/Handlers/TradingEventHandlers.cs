// Handlers/TradingEventHandlers.cs

using BusLibrary02.Core;
using TradingPlatform.Events;
using TradingPlatform.Services;

using Microsoft.Extensions.Logging;

namespace TradingPlatform.Handlers;

[Handles("system:*")]
public class SystemEventHandler : IMessageHandler<SystemStatusEvent>
{
    private readonly ILogger<SystemEventHandler> _logger;

    public SystemEventHandler(ILogger<SystemEventHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(SystemStatusEvent message, CancellationToken ct)
    {
        var logLevel = message.Status switch
        {
            "Error" => LogLevel.Error,
            "Warning" => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, "System event: {Component} - {Status}: {Message}",
            message.Component, message.Status, message.Message);

        return ValueTask.CompletedTask;
    }
}

// Простая реализация ITick для передачи в стратегии
public class SimpleTick : GS.Trade.Abstractions.ITick
{
    public string Ticker { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public DateTime DateTime { get; set; }
    public long TickNumber { get; set; }
}
