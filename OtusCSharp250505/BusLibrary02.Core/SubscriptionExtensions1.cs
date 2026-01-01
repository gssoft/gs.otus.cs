// SubscriptionExtensions.cs
/*
namespace BusLibrary02.Core;

public static class SubscriptionExtensions1
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
}
*/