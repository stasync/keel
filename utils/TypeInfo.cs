using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Keel.Utils
{
    /// <summary>
    /// An API that holds the information about any type.
    /// </summary>
    public static class TypeTraits
    {
        private static readonly Dictionary<Type, bool> s_blittableValueCache = new();

        /// <summary>
        /// Returns true, if type is blittable.
        /// </summary>
        /// <param name="type"></param>
        /// <returns>True, if type is blittable, false otherwise.</returns>
        public static bool IsBlittable(Type type)
        {
            if (s_blittableValueCache.TryGetValue(type, out var result))
                return result;

            var isBlittable = CheckThroughPinnedHandle() && IsTypeBlittableRecursive(type);
            s_blittableValueCache.Add(type, isBlittable);
            return isBlittable;

            // A quick checking though pinning, should sometimes help to avoid recursion.
            bool CheckThroughPinnedHandle()
            {
                if (!type.IsValueType)
                    return false;

                try
                {
                    // Attempt to allocate a pinned GCHandle on a dummy instance.
                    var obj = Activator.CreateInstance(type);
                    GCHandle.Alloc(obj, GCHandleType.Pinned).Free();
                    return true;
                }
                catch
                {
                    // ignored
                }

                return false;
            }
        }

        /// <summary>
        /// Primitive types: https://learn.microsoft.com/en-us/dotnet/api/system.type.isprimitive?view=net-7.0
        /// Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64, IntPtr, UIntPtr, Char, Double, Single.
        /// 
        /// Blittable types: https://learn.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types
        /// System.Byte, System.SByte, System.Int16, System.UInt16, System.Int32, System.UInt32, System.Int64, System.UInt64, System.IntPtr, System.UIntPtr, System.Single, System.Double
        /// So Boolean and Char are exceptions.
        /// </summary>
        /// <param name="type">Type to test.</param>
        /// <returns>True, if type is blittable, false otherwise.</returns>
        private static bool IsTypeBlittableRecursive(Type type)
        {
            if (!type.IsValueType)
                return false;

            if (type == typeof(char) || type == typeof(bool))
                return false;

            // No need to include a pointer, probably.
            if (type.IsPointer)
                return false;

            // For the rest of the primitive types, just return true.
            if (type.IsPrimitive || type.IsEnum)
                return true;

            // Type is not primitive, go through the nested fields.
            foreach (var field in type.GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                // Special case for bool fields.
                if (field.FieldType == typeof(bool))
                {
                    var marshalAsAttribute = field.GetCustomAttribute<MarshalAsAttribute>();
                    if (marshalAsAttribute is { Value: UnmanagedType.U1 })
                        continue;

                    return false;
                }

                if (!IsTypeBlittableRecursive(field.FieldType))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// An API that holds the information about <typeparam name="T"></typeparam> type.
    /// </summary>
    public static class TypeInfo<T>
    {
        public static readonly bool IsBlittable;

        static TypeInfo() =>
            IsBlittable = TypeTraits.IsBlittable(typeof(T));
    }
}