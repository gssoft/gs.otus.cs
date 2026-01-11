// EventHandlerRegistry.cs

namespace BusLibrary02.Core;

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

public sealed class EventHandlerRegistry : IEventHandlerRegistry
{
    private readonly Dictionary<string, List<Type>> _handlerMap;
    private readonly List<Type> _handlerTypes;

    public EventHandlerRegistry(IEnumerable<Assembly> assemblies)
    {
        _handlerMap = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
        _handlerTypes = new List<Type>();

        DiscoverHandlers(assemblies);
    }

    private void DiscoverHandlers(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => t.GetCustomAttributes<HandlesAttribute>().Any())
                    .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)));

                foreach (var type in types)
                {
                    _handlerTypes.Add(type);
                    var attributes = type.GetCustomAttributes<HandlesAttribute>();

                    foreach (var attr in attributes)
                    {
                        if (!_handlerMap.TryGetValue(attr.Key, out var list))
                        {
                            list = new List<Type>();
                            _handlerMap[attr.Key] = list;
                        }

                        if (!list.Contains(type))
                            list.Add(type);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine($"Ошибка загрузки типов из сборки {assembly}: {ex.Message}");
            }
        }
    }

    public void RegisterHandlers(IServiceCollection services)
    {
        foreach (var handlerType in _handlerTypes)
            services.AddSingleton(handlerType);
    }

    public IEnumerable<Type> GetHandlerTypes() => _handlerTypes;

    public System.Collections.Generic.IDictionary<string, List<Type>> GetHandlerMap() =>
        _handlerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
}


