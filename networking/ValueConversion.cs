using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Keel.Networking
{
    internal static class ValueConversion
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Union32 ToUnion(float value)
        {
            var u = default(Union32);
            u.FloatValue = value;
            return u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Union32 ToUnion(uint value)
        {
            var u = default(Union32);
            u.UInt32Value = value;
            return u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Union32 ToUnion(int value)
        {
            var u = default(Union32);
            u.Int32Value = value;
            return u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Union64 ToUnion(double value)
        {
            var u = default(Union64);
            u.DoubleValue = value;
            return u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Union64 ToUnion(ulong value)
        {
            var u = default(Union64);
            u.UInt64Value = value;
            return u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Union64 ToUnion(long value)
        {
            var u = default(Union64);
            u.Int64Value = value;
            return u;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct Union32
        {
            [FieldOffset(0)] public float FloatValue;
            [FieldOffset(0)] public uint UInt32Value;
            [FieldOffset(0)] public int Int32Value;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct Union64
        {
            [FieldOffset(0)] public double DoubleValue;
            [FieldOffset(0)] public ulong UInt64Value;
            [FieldOffset(0)] public long Int64Value;
        }
    }
}