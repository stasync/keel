using System.Collections.Generic;

namespace Keel.DependencyInjection.Events
{
    public interface IReadOnlyEventCollection : IReadOnlyCollection<KeyValuePair<int, IReadOnlyEvent>>
    {
    }
}