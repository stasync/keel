using Keel.DependencyInjection.Internal;

namespace Keel.DependencyInjection
{
    public class RootScope : Scope
    {
        /// <summary>
        /// Upon resolving, this context will temporarily hold all instances.
        /// </summary>
        internal DependencyResolvingContext DependencyResolvingContext;

        public RootScope(ScopeDependencyMap scopeDependencyMap = null) : base(parentScope: null, rootScope: null, scopeDependencyMap)
        {
        }
    }
}