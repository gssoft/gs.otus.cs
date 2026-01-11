// EventHub.cs

using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace BusLibrary02.Core;

// EventHub.cs ( 25.12.14 исправленный InProcessEventHub)
public sealed class InProcessEventHub : IEventHub, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IKeyRouter _router;
    private readonly IKeyCatalog? _catalog;
    private readonly Channel<IMessage> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private readonly ILogger<InProcessEventHub>? _logger;
    private bool _disposed;
    private readonly int dispose_timeout_in_seconds = 5;

    public InProcessEventHub(IServiceProvider serviceProvider, IKeyRouter router, IKeyCatalog? catalog = null, int capacity = 8192, ILogger<InProcessEventHub>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _router = router;
        _catalog = catalog;
        _logger = logger;
        _channel = Channel.CreateBounded<IMessage>(new BoundedChannelOptions(capacity) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
        _logger?.LogInformation("EventHub pump task starting...");
        _pump = Task.Run(RunAsync);
        _logger?.LogInformation("EventHub pump task started.");
    }

    public async ValueTask PublishAsync(IMessage message, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogDebug($"Publishing message with key '{message.Key}'.");
            await _channel.Writer.WriteAsync(message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("PublishAsync cancelled for message with key '{Key}'.", message.Key);
            // Не пробрасываем исключение дальше - это нормально при остановке
        }
        catch (ChannelClosedException)
        {
            _logger?.LogDebug("Channel closed, cannot publish message with key '{Key}'.", message.Key);
            // Канал закрыт, EventHub останавливается
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error publishing message with key '{Key}'.", message.Key);
            // Для других исключений можно пробросить дальше или обработать по-другому
            throw;
        }
    }

    public async ValueTask PublishAsync(long keyId, IMessage message, CancellationToken ct = default)
    {
        try
        {
            if (_catalog is null) throw new InvalidOperationException("IKeyCatalog is not configured.");
            if (!_catalog.TryGetString(keyId, out var key)) throw new KeyNotFoundException($"KeyId {keyId} not found in catalog.");
            if (!string.Equals(message.Key, key, StringComparison.OrdinalIgnoreCase)) message = new RoutedWrapper(message, key);
            await PublishAsync(message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("PublishAsync by keyId cancelled for keyId '{KeyId}'.", keyId);
            // Не пробрасываем исключение дальше
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or ChannelClosedException))
        {
            _logger?.LogError(ex, "Error publishing message by keyId '{KeyId}'.", keyId);
            throw;
        }
    }

    private async Task RunAsync()
    {
        _logger?.LogDebug("EventHub RunAsync loop started.");
        try
        {
            while (await _channel.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                _logger?.LogDebug("EventHub RunAsync: Channel has messages to read.");
                while (_channel.Reader.TryRead(out var message))
                {
                    _logger?.LogDebug($"EventHub RunAsync: Processing message with key '{message.Key}'.");
                    var handlers = _router.Resolve(_serviceProvider, message.Key);
                    _logger?.LogDebug($"EventHub RunAsync: Found {handlers.Count()} handlers for key '{message.Key}'.");

                    foreach (var h in handlers)
                    {
                        try
                        {
                            await h(_serviceProvider, message, _cts.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Не логируем TaskCanceledException как ошибку
                            if (ex is not (OperationCanceledException or TaskCanceledException))
                            {
                                _logger?.LogError(ex, "Error in message handler for key '{MessageKey}'.", message.Key);
                            }
                            else
                            {
                                _logger?.LogDebug("Handler cancelled for key '{MessageKey}'.", message.Key);
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("EventHub RunAsync loop cancelled.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "EventHub RunAsync loop error.");
        }
    }

    private sealed record RoutedWrapper(IMessage Inner, string NewKey) : IMessage
    {
        public string Key => NewKey;
        public string? SenderKey => Inner.SenderKey;
        public DateTimeOffset CreatedAt => Inner.CreatedAt;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _logger?.LogInformation("EventHub disposing, starting graceful shutdown...");

            // 1. Прекращаем принимать новые сообщения
            _channel.Writer.TryComplete();

            // 2. Даем время на обработку оставшихся сообщений
            var gracefulShutdownTimeout = TimeSpan.FromSeconds(3);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Ждем, пока канал не опустеет или не истечет время
            while (!_channel.Reader.Completion.IsCompleted &&
                   stopwatch.Elapsed < gracefulShutdownTimeout)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }

            _logger?.LogInformation("EventHub: Graceful shutdown completed, remaining messages: {Count}",
                _channel.Reader.Count);

            // 3. Отменяем токен
            _cts.Cancel();
        }
        catch (ObjectDisposedException) { return; }

        try
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(dispose_timeout_in_seconds));
            var completedTask = await Task.WhenAny(_pump, timeoutTask).ConfigureAwait(false);
            if (completedTask != _pump)
            {
                _logger?.LogWarning("EventHub disposal: Pump did not finish in time, forcing cancellation.");
                _cts.Cancel();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during EventHub disposal.");
        }
        finally
        {
            _cts.Dispose();
        }

        _logger?.LogInformation("EventHub disposed.");
    }
}

