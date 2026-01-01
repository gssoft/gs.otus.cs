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


//public sealed class EventHandlerRegistry : IEventHandlerRegistry
//{
//    private readonly Dictionary<string, List<Type>> _handlerMap;
//    private readonly List<Type> _handlerTypes;

//    public EventHandlerRegistry(IEnumerable<Assembly> assemblies)
//    {
//        _handlerMap = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
//        _handlerTypes = new List<Type>();

//        DiscoverHandlers(assemblies);
//    }

//    private void DiscoverHandlers(IEnumerable<Assembly> assemblies)
//    {
//        foreach (var assembly in assemblies)
//        {
//            try
//            {
//                var types = assembly.GetTypes()
//                    .Where(t => !t.IsAbstract && !t.IsInterface)
//                    .Where(t => t.GetCustomAttributes<HandlesAttribute>().Any())
//                    .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)));

//                foreach (var type in types)
//                {
//                    _handlerTypes.Add(type);
//                    var attributes = type.GetCustomAttributes<HandlesAttribute>();

//                    foreach (var attr in attributes)
//                    {
//                        if (!_handlerMap.TryGetValue(attr.Key, out var list))
//                        {
//                            list = new List<Type>();
//                            _handlerMap[attr.Key] = list;
//                        }

//                        if (!list.Contains(type))
//                            list.Add(type);
//                    }
//                }
//            }
//            catch (ReflectionTypeLoadException ex)
//            {
//                Console.WriteLine($"Ошибка загрузки типов из сборки {assembly}: {ex.Message}");
//            }
//        }
//    }

//    public void RegisterHandlers(IServiceCollection services)
//    {
//        foreach (var handlerType in _handlerTypes)
//            services.AddSingleton(handlerType);
//    }

//    public IEnumerable<Type> GetHandlerTypes() => _handlerTypes;

//    public System.Collections.Generic.IDictionary<string, List<Type>> GetHandlerMap() =>
//        _handlerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
//}

//using Microsoft.Extensions.DependencyInjection;
//using System.Reflection;

//public sealed class EventHandlerRegistry : IEventHandlerRegistry
//{
//    private readonly List<IEventHubModule> _modules;
//    private readonly Dictionary<string, List<Type>> _handlerMap;
//    private readonly List<Type> _handlerTypes;

//    public EventHandlerRegistry(IEnumerable<IEventHubModule> modules)
//    {
//        _modules = modules.ToList();
//        _handlerMap = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
//        _handlerTypes = new List<Type>();
//        DiscoverHandlers();
//    }

//    private void DiscoverHandlers()
//    {
//        var handlerAssemblies = _modules.SelectMany(m => m.GetHandlerAssemblies()).Distinct().ToArray();

//        foreach (var assembly in handlerAssemblies)
//        {
//            try
//            {
//                var types = assembly.GetTypes()
//                    .Where(t => !t.IsAbstract && !t.IsInterface)
//                    .Where(t => t.GetCustomAttributes<HandlesAttribute>().Any())
//                    // ИСПРАВЛЕНИЕ: заменил ImplementedInterfaces на GetInterfaces()
//                    .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)));

//                foreach (var type in types)
//                {
//                    _handlerTypes.Add(type);
//                    var attributes = type.GetCustomAttributes<HandlesAttribute>();

//                    foreach (var attr in attributes)
//                    {
//                        if (!_handlerMap.TryGetValue(attr.Key, out var list))
//                        {
//                            list = new List<Type>();
//                            _handlerMap[attr.Key] = list;
//                        }
//                        if (!list.Contains(type)) list.Add(type);
//                    }
//                }
//            }
//            catch (ReflectionTypeLoadException ex)
//            {
//                Console.WriteLine($"Ошибка загрузки типов из сборки {assembly}: {ex.Message}");
//            }
//        }
//    }

//    public void RegisterHandlers(IServiceCollection services)
//    {
//        foreach (var handlerType in _handlerTypes)
//            services.AddSingleton(handlerType);
//    }

//    public IEnumerable<Type> GetHandlerTypes() => _handlerTypes;

//    public System.Collections.Generic.IDictionary<string, List<Type>> GetHandlerMap() =>
//        _handlerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
//}

//public sealed class EventHandlerRegistry : IEventHandlerRegistry
//{
//    private readonly List<IEventHubModule> _modules;
//    private readonly Dictionary<string, List<Type>> _handlerMap;
//    private readonly List<Type> _handlerTypes;
//    public EventHandlerRegistry(IEnumerable<IEventHubModule> modules)
//    {
//        _modules = modules.ToList();
//        _handlerMap = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
//        _handlerTypes = new List<Type>();
//        DiscoverHandlers();
//    }
//    private void DiscoverHandlers()
//    {
//        var handlerAssemblies = _modules.SelectMany(m => m.GetHandlerAssemblies()).Distinct().ToArray();
//        foreach (var assembly in handlerAssemblies)
//        {
//            try
//            {
//                var types = assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface)
//                    .Where(t => t.GetCustomAttributes<HandlesAttribute>().Any())
//                    .Where(t => t.ImplementedInterfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)));
//                foreach (var type in types)
//                {
//                    _handlerTypes.Add(type);
//                    var attributes = type.GetCustomAttributes<HandlesAttribute>();
//                    foreach (var attr in attributes)
//                    {
//                        if (!_handlerMap.TryGetValue(attr.Key, out var list))
//                        {
//                            list = new List<Type>();
//                            _handlerMap[attr.Key] = list;
//                        }
//                        if (!list.Contains(type)) list.Add(type);
//                    }
//                }
//            }
//            catch (ReflectionTypeLoadException ex)
//            {
//                Console.WriteLine($"Ошибка загрузки типов из сборки {assembly}: {ex.Message}");
//            }
//        }
//    }
//    public void RegisterHandlers(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
//    {
//        foreach (var handlerType in _handlerTypes) services.AddSingleton(handlerType);
//    }
//    public IEnumerable<Type> GetHandlerTypes() => _handlerTypes;
//    public System.Collections.Generic.IDictionary<string, List<Type>> GetHandlerMap() => _handlerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
//}