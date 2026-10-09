using Keel.Utils.Debug;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Keel.DependencyInjection.Events.Internal
{
    internal sealed class EventListenerMethod
    {
        public readonly MethodInfo Method;
        public readonly Type EventType;
        public readonly Type DelegateType;
        public readonly int Channel;

        public EventListenerMethod(MethodInfo method, Type eventType, int channel)
        {
            Method = method;
            EventType = eventType;
            DelegateType = typeof(Action<>).MakeGenericType(eventType);
            Channel = channel;
        }

        public Delegate CreateDelegate(object target) =>
            Method.CreateDelegate(DelegateType, target);
    }

    internal static class EventListenerMethodCache
    {
        private const BindingFlags MethodFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly ConcurrentDictionary<Type, EventListenerMethod[]> s_methodCache = new();

        internal static EventListenerMethod[] GetMethods(Type targetType) =>
            s_methodCache.GetOrAdd(targetType, CollectMethods);

        private static EventListenerMethod[] CollectMethods(Type targetType)
        {
            var result = new List<EventListenerMethod>();
            var visited = new HashSet<(MethodInfo, int)>();

            // GetMethods() never returns private methods declared on base types, so walk the hierarchy.
            for (var type = targetType; type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var methodInfo in type.GetMethods(MethodFlags))
                {
                    var attribute = methodInfo.GetCustomAttribute<EventListenerAttribute>(inherit: false);
                    if (attribute == null)
                        continue;

                    // Each attribute is one subscription. The base and the override marked for the same channel are
                    // the same subscription, so it's registered once; the delegate dispatches virtually anyway.
                    if (!visited.Add((methodInfo.GetBaseDefinition(), attribute.Channel)))
                        continue;

                    if (!TryGetEventType(methodInfo, out var eventType))
                    {
                        Logger.LogError($"Invalid event listener '{type.FullName}.{methodInfo.Name}'. Make sure signature is defined as 'void {methodInfo.Name}(TEvent eventData)', where 'TEvent' is a struct and not a ref struct.");
                        continue;
                    }

                    // MakeGenericType can fail on AOT runtimes (e.g. IL2CPP) when Action<TEvent> was never generated.
                    // Skip just this listener, so the type is still cached and the error is logged once.
                    EventListenerMethod listener;
                    try
                    {
                        listener = new EventListenerMethod(methodInfo, eventType, attribute.Channel);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Skipping event listener '{type.FullName}.{methodInfo.Name}': unable to build Action<{eventType.FullName}>. {e.Message}");
                        continue;
                    }

                    result.Add(listener);
                }
            }

            return result.ToArray();
        }

        private static bool TryGetEventType(MethodInfo methodInfo, out Type eventType)
        {
            eventType = null;

            if (methodInfo.ReturnType != typeof(void) || methodInfo.ContainsGenericParameters)
                return false;

            var parameters = methodInfo.GetParameters();
            if (parameters.Length != 1)
                return false;

            // ByRef types (in/ref/out) are not value types, so they are rejected here too.
            // Nullable<T> and ref structs can't satisfy the 'struct' constraint of IBroadcaster.Invoke<T>, so they could never fire.
            var parameterType = parameters[0].ParameterType;
            if (!parameterType.IsValueType || parameterType.IsByRefLike || Nullable.GetUnderlyingType(parameterType) != null)
                return false;

            eventType = parameterType;
            return true;
        }
    }
}