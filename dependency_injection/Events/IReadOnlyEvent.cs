using System.Collections.Generic;

namespace Core.DependencyInjection.Events
{
    public interface IReadOnlyEvent
    {
        int ListenerCount { get; }
        IEnumerable<string> GetListeners();
    }
}