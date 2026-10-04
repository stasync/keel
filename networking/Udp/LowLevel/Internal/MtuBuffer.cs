using System;
using System.Buffers;

namespace Keel.Networking.Udp.LowLevel.Internal
{
    /// <summary>
    /// This class is thread-safe (based on <see cref="ArrayPool{Byte}"/>). All members may be used by multiple threads concurrently.
    /// https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1?view=net-9.0
    /// </summary>
    internal static class MtuBuffer
    {
        /// <summary>
        /// Standard Ethernet MTU (1500) - IP header (20) - UDP header (8) = 1472
        /// Reduced to 1452 for additional protocol overhead margin
        /// </summary>
        internal const int SIZE = 1452;

        /// <summary>
        /// Thread-safe array pool.
        /// </summary>
        private static readonly ArrayPool<byte> s_pool = ArrayPool<byte>.Create();

        /// <summary>
        /// Rents a buffer from the pool and copies data. 
        /// IMPORTANT: Caller MUST call Release() to return the buffer to the pool.
        /// </summary>
        internal static ArraySegment<byte> Rent(in ArraySegment<byte> src)
        {
#if DEBUG
            if (src.Array == null)
                throw new ArgumentNullException(nameof(src), "Source array cannot be null");
#endif
            var dstArray = s_pool.Rent(minimumLength: SIZE);

            var count = src.Count > SIZE ? SIZE : src.Count;
            Buffer.BlockCopy(src.Array!, src.Offset, dstArray, dstOffset: 0, count: count);
            return new ArraySegment<byte>(dstArray, offset: 0, count: count);
        }

        internal static void Release(in ArraySegment<byte> src)
        {
#if DEBUG
            if (src.Array == null)
                throw new ArgumentNullException(nameof(src), "Source array cannot be null");
#endif
            s_pool.Return(src.Array!);
        }
    }
}