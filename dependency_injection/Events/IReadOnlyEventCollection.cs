using System.Collections.Generic;

namespace Core.DependencyInjection.Events
{
    public interface IReadOnlyEventCollection : IReadOnlyCollection<KeyValuePair<int, IReadOnlyEvent>>
    {
    }
}