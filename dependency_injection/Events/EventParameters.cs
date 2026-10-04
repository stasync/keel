namespace Keel.DependencyInjection.Events
{
    public struct EventParameters
    {
        private readonly object[] _parameters;
        private int _currentPosition;

        public bool CanReadNext =>
            _currentPosition < _parameters.Length;

        public EventParameters(object[] parameters)
        {
            _parameters = parameters;
            _currentPosition = 0;
        }

        /// <summary>
        ///  TODO: Add boundaries check.
        /// </summary>
        public T Next<T>()
        {
            var element = (T)_parameters[_currentPosition];
            _currentPosition++;
            return element;
        }

        /// <summary>
        ///  TODO: Add boundaries check.
        /// </summary>
        public T At<T>(int index) =>
            (T)_parameters[index];
    }
}