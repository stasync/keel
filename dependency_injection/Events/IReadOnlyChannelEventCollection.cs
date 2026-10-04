using System.Collections.Generic;

namespace Keel.DependencyInjection.Events
{
    public interface IReadOnlyChannelEventCollection : IReadOnlyCollection<KeyValuePair<int, IReadOnlyEventCollection>>
    {
    }
}