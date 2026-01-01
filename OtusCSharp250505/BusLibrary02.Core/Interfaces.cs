// Interfaces.cs

namespace BusLibrary02.Core;

public interface IMessage
{
    string Key { get; }
    string? SenderKey { get; }
    DateTimeOffset CreatedAt { get; }
}

public interface IKeyCatalog
{
    bool TryGetString(long keyId, out string key);
    bool TryGetId(string key, out long keyId);
}

public interface IMessageHandler<in TMessage> where TMessage : IMessage
{
    ValueTask Handle(TMessage message, CancellationToken ct);
}

public interface IEventHub
{
    ValueTask PublishAsync(IMessage message, CancellationToken ct = default);
    ValueTask PublishAsync(long keyId, IMessage message, CancellationToken ct = default);
}

public interface IKeyRouter
{
    IEnumerable<Func<IServiceProvider, IMessage, CancellationToken, ValueTask>>
        Resolve(IServiceProvider serviceProvider, string key);
}

public interface IEventHubModule
{
    IEnumerable<System.Reflection.Assembly> GetHandlerAssemblies();
    IEnumerable<System.Reflection.Assembly> GetEventAssemblies();
    IEnumerable<KeyValuePair<string, string>>? GetStaticKeys() => null;
}

public interface IEventHandlerRegistry
{
    void RegisterHandlers(Microsoft.Extensions.DependencyInjection.IServiceCollection services);
    IEnumerable<Type> GetHandlerTypes();
    System.Collections.Generic.IDictionary<string, List<Type>> GetHandlerMap();
}


