using Core.Utils.Debug;
using System;
using System.Runtime.CompilerServices;

namespace Core.Networking
{
    public sealed class NetBuffer
    {
        private const int INITIAL_SIZE = 64;
        private const int BUFFER_SIZE_WARNING = 2048;
        private const float GROWTH_FACTOR = 1.5f;

        internal byte[] InternalBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InternalBuffer.Length;
        }

        public NetBuffer() =>
            InternalBuffer = new byte[INITIAL_SIZE];

        public NetBuffer(byte[] buffer) =>
            InternalBuffer = buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            if (Position >= InternalBuffer.Length)
                throw new IndexOutOfRangeException($"{nameof(ReadByte)} out of range: {ToString()}.");

            return InternalBuffer[Position++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBytes(byte[] buffer, int count)
        {
            if (count > buffer.Length)
                throw new IndexOutOfRangeException($"{nameof(ReadBytes)} out of range: ({count}): {ToString()}");

            if (Position + count > InternalBuffer.Length)
                throw new IndexOutOfRangeException($"{nameof(ReadBytes)} out of range: ({count}): {ToString()}");

            Buffer.BlockCopy(src: InternalBuffer, srcOffset: Position, dst: buffer, dstOffset: 0, count);
            Position += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe T ReadUnmanaged<T>() where T : unmanaged
        {
            var size = sizeof(T);
            if (Position + size > InternalBuffer.Length)
                throw new IndexOutOfRangeException($"{nameof(ReadBytes)} out of range: ({size}): {ToString()}");

            var result = default(T);

            fixed (byte* bufferPtr = InternalBuffer)
            {
                Buffer.MemoryCopy(source: bufferPtr + Position, destination: &result, destinationSizeInBytes: size, sourceBytesToCopy: size);
                Position += size;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadBytesAsArraySegment(int count)
        {
            if (Position + count > InternalBuffer.Length)
                throw new IndexOutOfRangeException($"{nameof(ReadBytesAsArraySegment)} out of range: ({count}): {ToString()}");

            var result = new ArraySegment<byte>(InternalBuffer, offset: Position, count);
            Position += count;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            EnsureEnoughSpaceToAdd(sizeToAdd: 1);
            InternalBuffer[Position] = value;
            Position += 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte2(byte value0, byte value1)
        {
            EnsureEnoughSpaceToAdd(sizeToAdd: 2);
            InternalBuffer[Position] = value0;
            InternalBuffer[Position + 1] = value1;
            Position += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte4(byte value0, byte value1, byte value2, byte value3)
        {
            EnsureEnoughSpaceToAdd(sizeToAdd: 4);
            InternalBuffer[Position] = value0;
            InternalBuffer[Position + 1] = value1;
            InternalBuffer[Position + 2] = value2;
            InternalBuffer[Position + 3] = value3;
            Position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte8(byte value0, byte value1, byte value2, byte value3, byte value4, byte value5, byte value6, byte value7)
        {
            EnsureEnoughSpaceToAdd(sizeToAdd: 8);
            InternalBuffer[Position] = value0;
            InternalBuffer[Position + 1] = value1;
            InternalBuffer[Position + 2] = value2;
            InternalBuffer[Position + 3] = value3;
            InternalBuffer[Position + 4] = value4;
            InternalBuffer[Position + 5] = value5;
            InternalBuffer[Position + 6] = value6;
            InternalBuffer[Position + 7] = value7;
            Position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(byte[] buffer, int srcOffset, int count)
        {
            EnsureEnoughSpaceToAdd(count);
            Buffer.BlockCopy(src: buffer, srcOffset, dst: InternalBuffer, dstOffset: Position, count);
            Position += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void WriteUnmanaged<T>(in T value) where T : unmanaged
        {
            var size = sizeof(T);
            EnsureEnoughSpaceToAdd(size);

            fixed (byte* bufferPtr = InternalBuffer)
            {
                fixed (T* dataPtr = &value)
                    Buffer.MemoryCopy(source: dataPtr, destination: bufferPtr + Position, destinationSizeInBytes: size, sourceBytesToCopy: size);
            }

            Position += size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SeekZero() =>
            Position = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Replace(byte[] buffer)
        {
            InternalBuffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Position = 0;
        }

        private void EnsureEnoughSpaceToAdd(int sizeToAdd)
        {
            if (sizeToAdd < 0)
                throw new InvalidOperationException();

            if (Position + sizeToAdd < InternalBuffer.Length)
                return;

            var newLength = (int)Math.Ceiling(InternalBuffer.Length * GROWTH_FACTOR);
            while (Position + sizeToAdd >= newLength)
            {
                newLength = (int)Math.Ceiling(newLength * GROWTH_FACTOR);
                if (newLength > BUFFER_SIZE_WARNING)
                    Logger.LogWarning($"{GetType().FullName} size is '{newLength}' bytes!");
            }

            var tmp = new byte[newLength];
            Buffer.BlockCopy(src: InternalBuffer, srcOffset: 0, dst: tmp, dstOffset: 0, count: Position);
            InternalBuffer = tmp;
        }

        public override string ToString() =>
            $"Size:{InternalBuffer.Length} Pos:{Position}";
    }
}