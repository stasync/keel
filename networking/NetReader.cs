using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Core.Networking
{
    public sealed class NetReader
    {
        private const int INITIAL_STRING_BUFFER_SIZE = 128;

        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Position;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length;
        }

        private readonly NetBuffer _buffer;
        private readonly UTF8Encoding _encoding = new();

        private byte[] _stringReaderBuffer = new byte[INITIAL_STRING_BUFFER_SIZE];

        public NetReader() =>
            _buffer = new NetBuffer();

        public NetReader(NetWriter writer) =>
            _buffer = new NetBuffer(writer.ToArray());

        public NetReader(byte[] buffer) =>
            _buffer = new NetBuffer(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Replace(byte[] buffer) =>
            _buffer.Replace(buffer);

        public uint ReadPackedUInt32()
        {
            var a0 = _buffer.ReadByte();
            if (a0 < 241)
                return a0;

            var a1 = _buffer.ReadByte();
            if (a0 <= 248)
                return (uint)(240 + 256 * (a0 - 241) + a1);

            var a2 = _buffer.ReadByte();
            if (a0 == 249)
                return (uint)(2288 + 256 * a1 + a2);

            var a3 = _buffer.ReadByte();
            if (a0 == 250)
                return a1 + ((uint)a2 << 8) + ((uint)a3 << 16);

            var a4 = _buffer.ReadByte();
            return a1 + ((uint)a2 << 8) + ((uint)a3 << 16) + ((uint)a4 << 24);
        }

        public ulong ReadPackedUInt64()
        {
            var a0 = _buffer.ReadByte();
            if (a0 < 241)
                return a0;

            var a1 = _buffer.ReadByte();
            if (a0 <= 248)
                return 240 + 256 * (a0 - (ulong)241) + a1;

            var a2 = _buffer.ReadByte();
            if (a0 == 249)
                return 2288 + (ulong)256 * a1 + a2;

            var a3 = _buffer.ReadByte();
            if (a0 == 250)
                return a1 + ((ulong)a2 << 8) + ((ulong)a3 << 16);

            var a4 = _buffer.ReadByte();
            if (a0 == 251)
                return a1 + ((ulong)a2 << 8) + ((ulong)a3 << 16) + ((ulong)a4 << 24);

            var a5 = _buffer.ReadByte();
            if (a0 == 252)
                return a1 + ((ulong)a2 << 8) + ((ulong)a3 << 16) + ((ulong)a4 << 24) + ((ulong)a5 << 32);

            var a6 = _buffer.ReadByte();
            if (a0 == 253)
                return a1 + ((ulong)a2 << 8) + ((ulong)a3 << 16) + ((ulong)a4 << 24) + ((ulong)a5 << 32) + ((ulong)a6 << 40);

            var a7 = _buffer.ReadByte();
            if (a0 == 254)
                return a1 + ((ulong)a2 << 8) + ((ulong)a3 << 16) + ((ulong)a4 << 24) + ((ulong)a5 << 32) + ((ulong)a6 << 40) + ((ulong)a7 << 48);

            var a8 = _buffer.ReadByte();
            return a1 + ((ulong)a2 << 8) + ((ulong)a3 << 16) + ((ulong)a4 << 24) + ((ulong)a5 << 32) + ((ulong)a6 << 40) + ((ulong)a7 << 48) + ((ulong)a8 << 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte() =>
            _buffer.ReadByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte() =>
            (sbyte)_buffer.ReadByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16()
        {
            ushort value = 0;
            value |= _buffer.ReadByte();
            value |= (ushort)(_buffer.ReadByte() << 8);
            return (short)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            ushort value = 0;
            value |= _buffer.ReadByte();
            value |= (ushort)(_buffer.ReadByte() << 8);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            uint value = 0;
            value |= _buffer.ReadByte();
            value |= (uint)(_buffer.ReadByte() << 8);
            value |= (uint)(_buffer.ReadByte() << 16);
            value |= (uint)(_buffer.ReadByte() << 24);
            return (int)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            uint value = 0;
            value |= _buffer.ReadByte();
            value |= (uint)(_buffer.ReadByte() << 8);
            value |= (uint)(_buffer.ReadByte() << 16);
            value |= (uint)(_buffer.ReadByte() << 24);
            return value;
        }

        public long ReadInt64()
        {
            ulong value = 0;

            ulong other = _buffer.ReadByte();
            value |= other;

            other = (ulong)_buffer.ReadByte() << 8;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 16;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 24;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 32;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 40;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 48;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 56;
            value |= other;

            return (long)value;
        }

        public ulong ReadUInt64()
        {
            ulong value = 0;
            ulong other = _buffer.ReadByte();
            value |= other;

            other = (ulong)_buffer.ReadByte() << 8;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 16;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 24;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 32;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 40;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 48;
            value |= other;

            other = (ulong)_buffer.ReadByte() << 56;
            value |= other;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingle()
        {
            var value = ReadUInt32();
            return ValueConversion.ToUnion(value).FloatValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            var value = ReadUInt64();
            return ValueConversion.ToUnion(value).DoubleValue;
        }

        public string ReadString()
        {
            var numBytes = ReadUInt16();
            if (numBytes == 0)
                return string.Empty;

            while (numBytes > _stringReaderBuffer.Length)
                _stringReaderBuffer = new byte[_stringReaderBuffer.Length * 2];

            _buffer.ReadBytes(_stringReaderBuffer, numBytes);
            return _encoding.GetString(_stringReaderBuffer, index: 0, count: numBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar() =>
            (char)_buffer.ReadByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean()
        {
            var value = _buffer.ReadByte();
            return value == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
                throw new IndexOutOfRangeException($"{nameof(ReadBytes)} '{count}' is out of range.");

            var value = new byte[count];
            _buffer.ReadBytes(value, count);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadBytesNoAlloc(int count)
        {
            if (count < 0)
                throw new IndexOutOfRangeException($"{nameof(ReadBytesNoAlloc)} '{count}' is out of range.");

            return _buffer.ReadBytesAsArraySegment(count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytesAndSize()
        {
            var size = ReadUInt16();
            return size == 0 ? Array.Empty<byte>() : ReadBytes(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadBytesAndSizeNonAlloc()
        {
            var size = ReadUInt16();
            return size == 0 ? new ArraySegment<byte>(Array.Empty<byte>()) : ReadBytesNoAlloc(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadUnmanaged<T>() where T : unmanaged =>
            _buffer.ReadUnmanaged<T>();

        public override string ToString() =>
            _buffer.ToString();
    }
}