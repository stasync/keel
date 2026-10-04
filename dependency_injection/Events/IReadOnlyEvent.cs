using System.Collections.Generic;

namespace Keel.DependencyInjection.Events
{
    public interface IReadOnlyEvent
    {
        int ListenerCount { get; }
        IEnumerable<string> GetListeners();
    }
}