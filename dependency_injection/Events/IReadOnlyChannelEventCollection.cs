using System.Collections.Generic;

namespace Core.DependencyInjection.Events
{
    public interface IReadOnlyChannelEventCollection : IReadOnlyCollection<KeyValuePair<int, IReadOnlyEventCollection>>
    {
    }
}