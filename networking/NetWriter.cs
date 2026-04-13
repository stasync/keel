using Core.Utils.Debug;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Core.Networking
{
    public sealed class NetWriter
    {
        private readonly NetBuffer _buffer = new();
        private readonly UTF8Encoding _encoding = new();

        public short Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (short)_buffer.Position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SeekZero() =>
            _buffer.SeekZero();

        public byte[] ToArray()
        {
            var newArray = new byte[_buffer.Position];
            Buffer.BlockCopy(src: _buffer.InternalBuffer, srcOffset: 0, dst: newArray, dstOffset: 0, count: _buffer.Position);
            return newArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> AsArraySegment() =>
            new(_buffer.InternalBuffer, offset: 0, count: Position);

        public void WritePackedUInt32(uint value)
        {
            switch (value)
            {
                case <= 240:
                    _buffer.WriteByte((byte)value);
                    return;
                case <= 2287:
                    _buffer.WriteByte((byte)((value - 240) / 256 + 241));
                    _buffer.WriteByte((byte)((value - 240) % 256));
                    return;
                case <= 67823:
                    _buffer.WriteByte(249);
                    _buffer.WriteByte((byte)((value - 2288) / 256));
                    _buffer.WriteByte((byte)((value - 2288) % 256));
                    return;
                case <= 16777215:
                    _buffer.WriteByte(250);
                    _buffer.WriteByte((byte)(value & 0xFF));
                    _buffer.WriteByte((byte)(value >> 8 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 16 & 0xFF));
                    return;
            }

            // all other values of uint
            _buffer.WriteByte(251);
            _buffer.WriteByte((byte)(value & 0xFF));
            _buffer.WriteByte((byte)(value >> 8 & 0xFF));
            _buffer.WriteByte((byte)(value >> 16 & 0xFF));
            _buffer.WriteByte((byte)(value >> 24 & 0xFF));
        }

        public void WritePackedUInt64(ulong value)
        {
            switch (value)
            {
                case <= 240:
                    _buffer.WriteByte((byte)value);
                    return;
                case <= 2287:
                    _buffer.WriteByte((byte)((value - 240) / 256 + 241));
                    _buffer.WriteByte((byte)((value - 240) % 256));
                    return;
                case <= 67823:
                    _buffer.WriteByte(249);
                    _buffer.WriteByte((byte)((value - 2288) / 256));
                    _buffer.WriteByte((byte)((value - 2288) % 256));
                    return;
                case <= 16777215:
                    _buffer.WriteByte(250);
                    _buffer.WriteByte((byte)(value & 0xFF));
                    _buffer.WriteByte((byte)(value >> 8 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 16 & 0xFF));
                    return;
                case <= 4294967295:
                    _buffer.WriteByte(251);
                    _buffer.WriteByte((byte)(value & 0xFF));
                    _buffer.WriteByte((byte)(value >> 8 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 16 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 24 & 0xFF));
                    return;
                case <= 1099511627775:
                    _buffer.WriteByte(252);
                    _buffer.WriteByte((byte)(value & 0xFF));
                    _buffer.WriteByte((byte)(value >> 8 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 16 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 24 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 32 & 0xFF));
                    return;
                case <= 281474976710655:
                    _buffer.WriteByte(253);
                    _buffer.WriteByte((byte)(value & 0xFF));
                    _buffer.WriteByte((byte)(value >> 8 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 16 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 24 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 32 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 40 & 0xFF));
                    return;
                case <= 72057594037927935:
                    _buffer.WriteByte(254);
                    _buffer.WriteByte((byte)(value & 0xFF));
                    _buffer.WriteByte((byte)(value >> 8 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 16 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 24 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 32 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 40 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 48 & 0xFF));
                    return;
                default:
                    _buffer.WriteByte(255);
                    _buffer.WriteByte((byte)(value & 0xFF));
                    _buffer.WriteByte((byte)(value >> 8 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 16 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 24 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 32 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 40 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 48 & 0xFF));
                    _buffer.WriteByte((byte)(value >> 56 & 0xFF));
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char value) =>
            _buffer.WriteByte((byte)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value) =>
            _buffer.WriteByte(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(sbyte value) =>
            _buffer.WriteByte((byte)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(short value) =>
            _buffer.WriteByte2((byte)(value & 0xff), (byte)(value >> 8 & 0xff));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort value) =>
            _buffer.WriteByte2((byte)(value & 0xff), (byte)(value >> 8 & 0xff));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int value) =>
            _buffer.WriteByte4(
                (byte)(value & 0xff),
                (byte)(value >> 8 & 0xff),
                (byte)(value >> 16 & 0xff),
                (byte)(value >> 24 & 0xff));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value) =>
            _buffer.WriteByte4(
                (byte)(value & 0xff),
                (byte)(value >> 8 & 0xff),
                (byte)(value >> 16 & 0xff),
                (byte)(value >> 24 & 0xff));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long value) =>
            _buffer.WriteByte8(
                (byte)(value & 0xff),
                (byte)(value >> 8 & 0xff),
                (byte)(value >> 16 & 0xff),
                (byte)(value >> 24 & 0xff),
                (byte)(value >> 32 & 0xff),
                (byte)(value >> 40 & 0xff),
                (byte)(value >> 48 & 0xff),
                (byte)(value >> 56 & 0xff));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value) =>
            _buffer.WriteByte8(
                (byte)(value & 0xff),
                (byte)(value >> 8 & 0xff),
                (byte)(value >> 16 & 0xff),
                (byte)(value >> 24 & 0xff),
                (byte)(value >> 32 & 0xff),
                (byte)(value >> 40 & 0xff),
                (byte)(value >> 48 & 0xff),
                (byte)(value >> 56 & 0xff));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSingle(float value)
        {
            var union = ValueConversion.ToUnion(value);
            WriteUInt32(union.UInt32Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            var union = ValueConversion.ToUnion(value);
            WriteUInt64(union.UInt64Value);
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                _buffer.WriteByte2(0, 0);
                return;
            }

            var count = _encoding.GetByteCount(value);
            var bytes = ArrayPool<byte>.Shared.Rent(count);
            try
            {
                WriteUInt16((ushort)count);
                var numBytes = _encoding.GetBytes(value, charIndex: 0, charCount: value.Length, bytes, byteIndex: 0);
                _buffer.WriteBytes(bytes, srcOffset: 0, count: numBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolean(bool value) =>
            _buffer.WriteByte(value ? (byte)1 : (byte)0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(byte[] buffer, int count) =>
            _buffer.WriteBytes(buffer, srcOffset: 0, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesAndSize(byte[] buffer) =>
            WriteBytesAndSize(buffer, buffer.Length);

        public void WriteBytesAndSize(byte[] buffer, int count)
        {
            if (buffer == null || count == 0)
            {
                WriteUInt16(0);
                return;
            }

            if (count > ushort.MaxValue)
            {
                Logger.LogError($"{GetType().Name}.{nameof(WriteBytesAndSize)}: buffer is too large ({count}) bytes.");
                return;
            }

            WriteUInt16((ushort)count);
            _buffer.WriteBytes(buffer, srcOffset: 0, count);
        }

        public void WriteBytesAndSize(in ArraySegment<byte> buffer)
        {
            if (buffer.Array == null || buffer.Count == 0)
            {
                WriteUInt16(0);
                return;
            }

            if (buffer.Count > ushort.MaxValue)
            {
                Logger.LogError($"{GetType().Name}.{nameof(WriteBytesAndSize)}: buffer is too large ({buffer.Count}) bytes.");
                return;
            }

            WriteUInt16((ushort)buffer.Count);
            _buffer.WriteBytes(buffer.Array, srcOffset: buffer.Offset, buffer.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanaged<T>(in T value) where T : unmanaged =>
            _buffer.WriteUnmanaged(value);
    }
}