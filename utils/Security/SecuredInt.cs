using System;

namespace Core.Utils.Security
{
    public readonly struct SecuredInt : IComparable<SecuredInt>, IEquatable<SecuredInt>
    {
        private readonly int _valueOffset;
        private readonly int _value;

        private SecuredInt(int value)
        {
            _valueOffset = ProduceRandomValue();
            _value = value ^ _valueOffset;
        }

        private static int ProduceRandomValue() =>
            Math.RandomExtended.Default.Int(-1000, 1000);

        private int GetRealValue() => _value ^ _valueOffset;

        public static implicit operator SecuredInt(int value) => new(value);
        public static implicit operator int(SecuredInt value) => value.GetRealValue();

        public static SecuredInt operator +(SecuredInt a, SecuredInt b) => (int)a + (int)b;
        public static SecuredInt operator -(SecuredInt a, SecuredInt b) => (int)a - (int)b;
        public static SecuredInt operator *(SecuredInt a, SecuredInt b) => (int)a * (int)b;
        public static SecuredInt operator /(SecuredInt a, SecuredInt b) => (int)a / (int)b;
        public static SecuredInt operator ++(SecuredInt value) => (int)value + 1;
        public static SecuredInt operator --(SecuredInt value) => (int)value - 1;

        public static bool operator ==(SecuredInt lhs, SecuredInt rhs) =>
            (int)lhs == (int)rhs;

        public static bool operator !=(SecuredInt lhs, SecuredInt rhs) =>
            (int)lhs != (int)rhs;

        public int CompareTo(SecuredInt other) => GetRealValue().CompareTo(other.GetRealValue());
        public bool Equals(SecuredInt other) => GetRealValue() == other.GetRealValue();
        public override bool Equals(object obj) => obj is SecuredInt other && Equals(other);
        public override int GetHashCode() => GetRealValue().GetHashCode();
        public override string ToString() => GetRealValue().ToString();
    }
}