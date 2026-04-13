using System;
using System.Collections.Generic;
using System.Reflection;

namespace Core.Utils
{
    public static class Types
    {
        public static IReadOnlyList<Type> AllTracked => s_allTracked;
        private static readonly List<Type> s_allTracked = new();
        private static readonly Dictionary<Type, int> s_typeToStableHashCode = new();

        static Types()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (ShouldSkipAssembly(assembly))
                        continue;

                    foreach (var type in assembly.GetTypes())
                    {
                        try
                        {
                            var stableHashCode = type.GetStableHashCode();
                            s_typeToStableHashCode.Add(type, stableHashCode);
                            s_allTracked.Add(type);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
            return;

            static bool ShouldSkipAssembly(Assembly assembly)
            {
                if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.FullName))
                    return true;

                // Exclude system and third-party assemblies
                return
                    assembly.FullName.StartsWith("System.") ||
                    assembly.FullName.StartsWith("System,") ||
                    assembly.FullName.StartsWith("Microsoft.") ||
                    assembly.FullName.StartsWith("netstandard") ||
                    assembly.FullName.StartsWith("mscorlib") ||
                    assembly.FullName.StartsWith("WindowsBase") ||
                    assembly.FullName.StartsWith("PresentationCore") ||
                    assembly.FullName.StartsWith("PresentationFramework") ||
                    assembly.FullName.StartsWith("Newtonsoft.") ||
                    assembly.FullName.StartsWith("NuGet.") ||
                    assembly.FullName.StartsWith("JetBrains.");
            }
        }

        public static bool TryGetTypeStableHashCode<TType>(out int hashCode) =>
            TryGetTypeStableHashCode(typeof(TType), out hashCode);

        public static bool TryGetTypeStableHashCode(Type type, out int hashCode) =>
            s_typeToStableHashCode.TryGetValue(type, out hashCode);

        public static class Lookup<TParent>
        {
            public static IReadOnlyList<Type> Value => s_value;
            private static readonly List<Type> s_value = new();

            private static readonly Dictionary<int, Type> s_stableHashCodeToType = new();
            private static readonly Dictionary<byte, Type> s_indexToType = new();
            private static readonly Dictionary<int, byte> s_stableHashCodeToIndex = new();

            static Lookup()
            {
                var parentType = typeof(TParent);
                foreach (var type in s_allTracked)
                {
                    if (parentType == type || type.IsInterface || type.IsAbstract || !parentType.IsAssignableFrom(type))
                        continue;

                    if (type.IsGenericType)
                        continue;

                    s_value.Add(type);
                    s_stableHashCodeToType.Add(type.GetStableHashCode(), type);
                }

                s_value.Sort(comparison: (type0, type1) =>
                    type0.GetStableHashCode().CompareTo(type1.GetStableHashCode()));

                for (var i = 0; i < s_value.Count; i++)
                {
                    s_stableHashCodeToIndex.Add(s_value[i].GetStableHashCode(), (byte)i);
                    s_indexToType.Add((byte)i, s_value[i]);
                }

                if (s_value.Count > byte.MaxValue)
                    throw new InvalidOperationException($"Too many child types ({s_value.Count}) for '{typeof(TParent).FullName}'. Maximum is '{byte.MaxValue}'.");
            }

            public static bool WithHashCode(int hashCode, out Type componentType) =>
                s_stableHashCodeToType.TryGetValue(hashCode, out componentType);

            public static bool WithIndex(byte typeIndex, out Type componentType) =>
                s_indexToType.TryGetValue(typeIndex, out componentType);

            public static byte IndexOf<TType>() where TType : TParent
            {
                if (!TryGetTypeStableHashCode<TType>(out var stableHashCode))
                    throw new InvalidOperationException($"Type '{typeof(TType).FullName}' is unknown type.");

                return s_stableHashCodeToIndex[stableHashCode];
            }

            public static byte IndexOf(Type type)
            {
                if (!TryGetTypeStableHashCode(type, out var stableHashCode))
                    throw new InvalidOperationException($"Type '{type.FullName}' is unknown type.");

                if (!s_stableHashCodeToIndex.TryGetValue(stableHashCode, out var index))
                    throw new InvalidOperationException($"Type '{type.FullName}' is not a child of '{typeof(TParent).FullName}'");

                return index;
            }
        }
    }
}