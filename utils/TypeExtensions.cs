using System;
using System.Collections.Generic;

namespace Core.Utils
{
    public static class TypeExtensions
    {
        private static readonly object s_locker = new();
        private static readonly Dictionary<Type, int> s_stableHashCodeCache = new();

        public static int GetStableHashCode(this Type type)
        {
            lock (s_locker)
            {
                // https://andrewlock.net/why-is-string-gethashcode-different-each-time-i-run-my-program-in-net-core/
                // https://stackoverflow.com/questions/5154970/how-do-i-create-a-hashcode-in-net-c-for-a-string-that-is-safe-to-store-in-a
                if (s_stableHashCodeCache.TryGetValue(type, out var hashCode))
                    return hashCode;

                var typeName = type.FullName ?? type.ToString();
                unchecked
                {
                    var hash = 23;
                    foreach (var c in typeName)
                        hash = hash * 31 + c;

                    s_stableHashCodeCache.Add(type, hash);
                    return hash;
                }
            }
        }

        public static bool IsBlittable(this Type type) =>
            TypeInfo.IsBlittable(type);
    }
}