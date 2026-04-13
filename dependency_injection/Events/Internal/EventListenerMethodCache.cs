using Core.Utils.Debug;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Core.DependencyInjection.Events.Internal
{
    internal static class EventListenerMethodCache
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<(MethodInfo, EventListenerAttribute)>> s_methodCache = new();

        internal static IReadOnlyList<(MethodInfo, EventListenerAttribute)> GetMethods(Type targetType)
        {
            if (s_methodCache.TryGetValue(targetType, out var cachedResult))
                return cachedResult;

            var result = new List<(MethodInfo, EventListenerAttribute)>();
            s_methodCache.TryAdd(targetType, result);

            foreach (var methodInfo in targetType.GetMethods(bindingAttr: BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var attribute = methodInfo.GetCustomAttribute<EventListenerAttribute>(inherit: false);
                if (attribute == null)
                    continue;

                var parameters = methodInfo.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(object[]))
                {
                    Logger.LogError($"Invalid method info parameters for method '{methodInfo.Name}'. Make sure signature is defined as '{methodInfo.Name}(params object[] args)'.");
                    continue;
                }

                result.Add((methodInfo, attribute));
            }

            return result;
        }
    }
}