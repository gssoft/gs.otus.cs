// DynamicSubscriptions.cs (исправленная версия)

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace BusLibrary02.Core;

/// <summary>
/// Интерфейс для динамической подписки на события
/// </summary>
public interface IDynamicSubscriptionManager
{
    /// <summary>
    /// Подписаться на событие по ключу
    /// </summary>
    IDisposable Subscribe<TMessage>(string key, Func<TMessage, CancellationToken, ValueTask> handler)
        where TMessage : IMessage;

    /// <summary>
    /// Подписаться на событие по типу сообщения (используя атрибут)
    /// </summary>
    IDisposable Subscribe<TMessage>(Func<TMessage, CancellationToken, ValueTask> handler)
        where TMessage : IMessage;

    /// <summary>
    /// Отписаться от события по ключу
    /// </summary>
    void Unsubscribe(string key);

    /// <summary>
    /// Отписаться от события по типу сообщения
    /// </summary>
    void Unsubscribe<TMessage>() where TMessage : IMessage;

    IEnumerable<string> GetSubscribedKeys();
}

/// <summary>
/// Менеджер динамических подписок
/// </summary>
public sealed class DynamicSubscriptionManager : IDynamicSubscriptionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, List<SubscriptionEntry>> _subscriptions = new();
    private readonly ConcurrentDictionary<Type, string> _typeToKeyMap = new();
    private readonly ConcurrentDictionary<Type, string> _typeToStaticKeyMap = new();
    private readonly ILogger<DynamicSubscriptionManager>? _logger;
    private bool _disposed;

    private class SubscriptionEntry
    {
        public Type MessageType { get; }
        public Delegate HandlerDelegate { get; }
        public Guid Id { get; }

        public SubscriptionEntry(Type messageType, Delegate handlerDelegate, Guid id)
        {
            MessageType = messageType;
            HandlerDelegate = handlerDelegate;
            Id = id;
        }
    }

    public DynamicSubscriptionManager(ILogger<DynamicSubscriptionManager>? logger = null)
    {
        _logger = logger;

        // Инициализируем статические ключи для стандартных типов сообщений
        InitializeStaticKeys();
    }

    private void InitializeStaticKeys()
    {
        // Пример добавления статических ключей для известных типов
        // Можно добавить здесь или использовать конфигурацию
    }

    /// <summary>
    /// Подписаться на событие по ключу
    /// </summary>
    public IDisposable Subscribe<TMessage>(string key, Func<TMessage, CancellationToken, ValueTask> handler)
        where TMessage : IMessage
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var entry = new SubscriptionEntry(
            typeof(TMessage),
            handler,
            Guid.NewGuid()
        );

        var subscriptions = _subscriptions.GetOrAdd(key, _ => new List<SubscriptionEntry>());

        lock (subscriptions)
        {
            subscriptions.Add(entry);
        }

        _logger?.LogDebug("Subscribed to key '{Key}' with handler ID {HandlerId}", key, entry.Id);

        // Сохраняем связь типа с ключом для возможности отписки по типу
        _typeToKeyMap.AddOrUpdate(typeof(TMessage), key, (_, existing) => existing);

        return new SubscriptionToken(this, key, entry.Id);
    }

    /// <summary>
    /// Подписаться на событие по типу сообщения
    /// </summary>
    public IDisposable Subscribe<TMessage>(Func<TMessage, CancellationToken, ValueTask> handler)
        where TMessage : IMessage
    {
        // Попробуем получить ключ из статического словаря
        if (!_typeToStaticKeyMap.TryGetValue(typeof(TMessage), out var key))
        {
            // Если ключ не найден, попробуем получить его из атрибута
            var attr = typeof(TMessage).GetCustomAttributes(false)
                .OfType<MessageKeyAttribute>()
                .FirstOrDefault();

            if (attr != null)
            {
                key = attr.Key;
                _typeToStaticKeyMap.TryAdd(typeof(TMessage), key);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot subscribe to {typeof(TMessage).Name} without a key. " +
                    $"Either use Subscribe(key, handler) or add [MessageKey] attribute to the message type.");
            }
        }

        return Subscribe(key, handler);
    }

    /// <summary>
    /// Зарегистрировать статический ключ для типа сообщения
    /// </summary>
    public void RegisterStaticKey<TMessage>(string key) where TMessage : IMessage
    {
        _typeToStaticKeyMap[typeof(TMessage)] = key;
    }

    
    

    /// <summary>
    /// Отписаться от события по ключу
    /// </summary>
    public void Unsubscribe(string key)
    {
        if (_subscriptions.TryRemove(key, out var subscriptions))
        {
            _logger?.LogDebug("Unsubscribed from key '{Key}', removed {Count} handlers",
                key, subscriptions.Count);
        }
    }

    /// <summary>
    /// Отписаться от события по типу сообщения
    /// </summary>
    public void Unsubscribe<TMessage>() where TMessage : IMessage
    {
        if (_typeToKeyMap.TryGetValue(typeof(TMessage), out var key))
        {
            Unsubscribe(key);
            _typeToKeyMap.TryRemove(typeof(TMessage), out _);
        }
    }

    /// <summary>
    /// Внутренний метод для отписки по ID обработчика
    /// </summary>
    private bool Unsubscribe(string key, Guid handlerId)
    {
        if (_subscriptions.TryGetValue(key, out var subscriptions))
        {
            lock (subscriptions)
            {
                var removed = subscriptions.RemoveAll(x => x.Id == handlerId);
                if (removed > 0)
                {
                    // Если обработчиков не осталось, удаляем ключ
                    if (subscriptions.Count == 0)
                    {
                        _subscriptions.TryRemove(key, out _);
                    }

                    _logger?.LogDebug("Unsubscribed handler {HandlerId} from key '{Key}'",
                        handlerId, key);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Получить все обработчики для указанного ключа
    /// </summary>
    public IEnumerable<Func<IServiceProvider, IMessage, CancellationToken, ValueTask>>
        GetHandlers(string key)
    {
        if (!_subscriptions.TryGetValue(key, out var subscriptions))
            return Enumerable.Empty<Func<IServiceProvider, IMessage, CancellationToken, ValueTask>>();

        var handlers = new List<Func<IServiceProvider, IMessage, CancellationToken, ValueTask>>();

        lock (subscriptions)
        {
            foreach (var entry in subscriptions)
            {
                // Создаем обертку, которая правильно приводит типы
                handlers.Add(CreateHandlerWrapper(entry));
            }
        }

        return handlers;
    }

    private Func<IServiceProvider, IMessage, CancellationToken, ValueTask>
        CreateHandlerWrapper(SubscriptionEntry entry)
    {
        return (sp, msg, ct) =>
        {
            // Проверяем тип сообщения
            if (!entry.MessageType.IsInstanceOfType(msg))
            {
                throw new InvalidCastException(
                    $"Cannot cast message of type {msg.GetType()} to {entry.MessageType}");
            }

            // Получаем метод DynamicInvoke у делегата
            var result = entry.HandlerDelegate.DynamicInvoke(msg, ct);

            // Возвращаем ValueTask
            if (result is ValueTask valueTask)
            {
                return valueTask;
            }
            else if (result is Task task)
            {
                return new ValueTask(task);
            }
            else
            {
                return ValueTask.CompletedTask;
            }
        };
    }

    /// <summary>
    /// Получить все зарегистрированные ключи
    /// </summary>
    public IEnumerable<string> GetSubscribedKeys() => _subscriptions.Keys;

    /// <summary>
    /// Получить количество подписок для ключа
    /// </summary>
    public int GetSubscriptionCount(string key)
    {
        return _subscriptions.TryGetValue(key, out var subscriptions)
            ? subscriptions.Count
            : 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _subscriptions.Clear();
            _typeToKeyMap.Clear();
            _typeToStaticKeyMap.Clear();
            _disposed = true;
            _logger?.LogInformation("DynamicSubscriptionManager disposed");
        }
    }

    /// <summary>
    /// Токен подписки для отписки через Dispose
    /// </summary>
    private sealed class SubscriptionToken : IDisposable
    {
        private readonly DynamicSubscriptionManager _manager;
        private readonly string _key;
        private readonly Guid _handlerId;
        private bool _disposed;

        public SubscriptionToken(DynamicSubscriptionManager manager, string key, Guid handlerId)
        {
            _manager = manager;
            _key = key;
            _handlerId = handlerId;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _manager.Unsubscribe(_key, _handlerId);
                _disposed = true;
            }
        }
    }
}
