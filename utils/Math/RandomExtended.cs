using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Core.Utils.Math
{
    public static class RandomExtended<T> where T : RandomExtended, new()
    {
        [ThreadStatic] private static T s_value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get() =>
            s_value ??= new T();
    }

    public class RandomExtended
    {
        public static RandomExtended Default
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RandomExtended<RandomExtended>.Get();
        }

        public RandomExtended(int seed) =>
            _internalRandom = new Random(seed);

        public RandomExtended() =>
            _internalRandom = new Random();

        private readonly Random _internalRandom;

        public float Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (float)_internalRandom.NextDouble();
        }

        /// <summary>
        /// Pseudo unique string.
        /// </summary>
        public string GetUniqueIdString()
        {
            var ticks = (DateTime.UtcNow - DateTime.UnixEpoch).Ticks;
            var id = ticks.ToString();

            id += String(length: 5);
            id += Int(1000, 9999);

            var chars = Shuffle(origin: id.ToCharArray());
            var str = new string(chars);

            return str[..System.Math.Min(5, str.Length)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string NumString(int length) =>
            String(format: "0123456789", length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string String(int length) =>
            String(format: "abcdefghijklmnopqrstuvwxyzABCDEFGHJKLMNOPQRSTUVWXYZ0123456789", length);

        public string String(string format, int length)
        {
            var chars = new char[length];
            for (var i = 0; i < length; i++)
                chars[i] = format[_internalRandom.Next(0, format.Length)];
            return new string(chars);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ArrayIndex(Array array) =>
            Int(0, array.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Int() =>
            _internalRandom.Next(int.MinValue, int.MaxValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Int(int threshold) =>
            _internalRandom.Next(-threshold, threshold);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Int(int min, int max) =>
            _internalRandom.Next(min, max);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetBytes(int length)
        {
            var bytes = new byte[length];
            _internalRandom.NextBytes(bytes);
            return bytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Float(float threshold) =>
            2f * (float)_internalRandom.NextDouble() * threshold - threshold;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Float(float min, float max) =>
            (float)_internalRandom.NextDouble() * (max - min) + min;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Bool() =>
            _internalRandom.Next(maxValue: 100) % 2 == 0;

        /// <summary>
        /// True percentage (0 - 100).
        /// </summary>
        public bool BoolAll(float truePercentage, int attempts)
        {
            for (var i = 0; i < attempts; i++)
            {
                if (!Bool(truePercentage))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// True percentage (0 - 100).
        /// </summary>
        public bool BoolAny(float truePercentage, int attempts)
        {
            for (var i = 0; i < attempts; i++)
            {
                if (Bool(truePercentage))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True percentage (0 - 100).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Bool(float truePercentage) =>
            _internalRandom.NextDouble() < truePercentage * 0.01f;

        public T[] Shuffle<T>(T[] origin)
        {
            var newArray = (T[])origin.Clone();
            for (var i = newArray.Length; i > 1; i--)
            {
                var j = Int(0, i);
                (newArray[j], newArray[i - 1]) = (newArray[i - 1], newArray[j]);
            }

            return newArray;
        }

        public List<T> Shuffle<T>(List<T> list) =>
            new(collection: Shuffle(list.ToArray()));

        public T RandomEnum<T>()
        {
            var values = (T[])Enum.GetValues(typeof(T));
            return values[Int(0, values.Length)];
        }
    }
}