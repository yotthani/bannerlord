using System;

namespace DualWieldPrototype
{
    internal static class DualWieldPrototypeTraceContext
    {
        [ThreadStatic]
        private static string _currentScope;

        public static string CurrentScope => _currentScope;

        public static IDisposable Push(string scope)
        {
            string previousScope = _currentScope;
            _currentScope = scope;
            return new Scope(previousScope);
        }

        private sealed class Scope : IDisposable
        {
            private readonly string _previousScope;
            private bool _disposed;

            public Scope(string previousScope)
            {
                _previousScope = previousScope;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _currentScope = _previousScope;
                _disposed = true;
            }
        }
    }
}
