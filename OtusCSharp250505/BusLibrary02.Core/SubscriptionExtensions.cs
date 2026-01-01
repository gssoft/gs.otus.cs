// SubscriptionExtensions.cs (исправленные)

namespace BusLibrary02.Core;

public static class SubscriptionExtensions
{
    /// <summary>
    /// Подписаться на событие по ключу (синхронный обработчик)
    /// </summary>
    public static IDisposable Subscribe<TMessage>(
        this IDynamicSubscriptionManager manager,
        string key,
        Action<TMessage> handler)
        where TMessage : IMessage
    {
        return manager.Subscribe<TMessage>(key, (msg, ct) =>
        {
            handler(msg);
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// Подписаться на событие по типу сообщения (синхронный обработчик)
    /// </summary>
    public static IDisposable Subscribe<TMessage>(
        this IDynamicSubscriptionManager manager,
        Action<TMessage> handler)
        where TMessage : IMessage
    {
        return manager.Subscribe<TMessage>((msg, ct) =>
        {
            handler(msg);
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// Подписаться на событие по ключу (асинхронный обработчик без CancellationToken)
    /// </summary>
    public static IDisposable Subscribe<TMessage>(
        this IDynamicSubscriptionManager manager,
        string key,
        Func<TMessage, ValueTask> handler)
        where TMessage : IMessage
    {
        return manager.Subscribe<TMessage>(key, async (msg, ct) =>
        {
            await handler(msg);
        });
    }

    /// <summary>
    /// Подписаться на событие по типу сообщения (асинхронный обработчик без CancellationToken)
    /// </summary>
    public static IDisposable Subscribe<TMessage>(
        this IDynamicSubscriptionManager manager,
        Func<TMessage, ValueTask> handler)
        where TMessage : IMessage
    {
        return manager.Subscribe<TMessage>(async (msg, ct) =>
        {
            await handler(msg);
        });
    }

    /// <summary>
    /// Зарегистрировать статический ключ для типа сообщения
    /// </summary>
    public static void RegisterStaticKey<TMessage>(
        this IDynamicSubscriptionManager manager,
        string key) where TMessage : IMessage
    {
        if (manager is DynamicSubscriptionManager dsm)
        {
            dsm.RegisterStaticKey<TMessage>(key);
        }
        else
        {
            throw new InvalidOperationException("Manager does not support static key registration");
        }
    }
}