using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Keel.DependencyInjection.Internal
{
    internal static class InjectionTargetsCache
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<FieldInfo>> s_fieldCache = new();

        internal static IReadOnlyList<FieldInfo> GetFields(Type implementationType)
        {
            if (s_fieldCache.TryGetValue(implementationType, out var cachedResult))
                return cachedResult;

            var result = new List<FieldInfo>();
            s_fieldCache.TryAdd(implementationType, result);

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var injectAttributeType = typeof(InjectAttribute);

            foreach (var field in implementationType.GetFields(flags))
            {
                if (Attribute.IsDefined(field, injectAttributeType))
                    result.Add(field);
            }

            foreach (var property in implementationType.GetProperties(flags))
            {
                if (!Attribute.IsDefined(property, injectAttributeType))
                    continue;

                var backingField = implementationType.GetField($"<{property.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (backingField != null)
                    result.Add(backingField);
                else
                    throw new InvalidOperationException($"Setter is not declared for property '{implementationType.FullName}.{property.Name}'.");
            }

            return result;
        }
    }
}