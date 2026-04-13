using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Core.Networking.Udp.LowLevel
{
    public sealed partial class UdpReliableProtocol
    {
        /// <summary>
        /// Keeps temporary banned/restricted (for what ever reason) ip addresses.
        /// Used to filter out the traffic if remote address sends incorrect/corrupted data.
        /// </summary>
        private sealed class IpAddressBlacklist
        {
            /// <summary>
            /// Key - address.
            /// Value - datetime when it should be removed from blacklist.
            /// </summary>
            private readonly Dictionary<IPAddress, DateTime> _blacklist = new();
            private readonly List<IPAddress> _addressesToRemove = new();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool IsBlacklisted(IPAddress address) =>
                _blacklist.ContainsKey(address);

            internal void Update()
            {
                if (_blacklist.Count == 0)
                    return;

                foreach ((var ipAddress, var dateTime) in _blacklist)
                {
                    if (DateTime.UtcNow > dateTime)
                        _addressesToRemove.Add(ipAddress);
                }

                foreach (var addressToRemove in _addressesToRemove)
                    _blacklist.Remove(addressToRemove);

                _addressesToRemove.Clear();
            }

            internal void AddOrUpdateBlacklistTime(IPAddress address, int millisecondsToAdd)
            {
                if (millisecondsToAdd <= 0)
                    return;

#if NET9_0_OR_GREATER
                ref var dateTimeValue = ref System.Runtime.InteropServices.CollectionsMarshal
                    .GetValueRefOrAddDefault(_blacklist, address, out var hasValue);

                if (!hasValue)
                    dateTimeValue = DateTime.UtcNow;

                dateTimeValue = dateTimeValue.AddMilliseconds(millisecondsToAdd);
#else
                if (_blacklist.TryGetValue(address, out DateTime value))
                    _blacklist[address] = value.AddMilliseconds(millisecondsToAdd);
                else
                    _blacklist.Add(address, DateTime.UtcNow.AddMilliseconds(millisecondsToAdd));
#endif
            }
        }
    }
}