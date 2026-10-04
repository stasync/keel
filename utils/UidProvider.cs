namespace Keel.Utils
{
    public sealed class UidProvider
    {
        private readonly object _lock = new();
        private uint _lastUid;

        public uint Next()
        {
            lock (_lock)
                return ++_lastUid;
        }
    }
}