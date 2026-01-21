using System;
using System.Collections.Concurrent;

namespace MagicDI
{
    /// <summary>
    /// A lightweight dependency injection container that uses reflection
    /// to automatically resolve constructor dependencies.
    /// </summary>
    public class MagicDI
    {
        private readonly object _singletonLock = new();
        private readonly ConcurrentDictionary<Type, object> _singletons = new();

        /// <summary>
        /// Resolves lifetime for types based on metadata analysis.
        /// </summary>
        private readonly LifetimeResolver _lifetimeResolver = new();

        /// <summary>
        /// Creates instances of types by resolving constructor dependencies.
        /// </summary>
        private readonly InstanceFactory _instanceFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="MagicDI"/> class.
        /// </summary>
        public MagicDI()
        {
            _instanceFactory = new InstanceFactory(Resolve);
        }

        /// <summary>
        /// Resolves an instance of the specified type, automatically
        /// resolving all constructor dependencies.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <returns>An instance of the specified type.</returns>
        public T Resolve<T>()
        {
            var resolved = Resolve(typeof(T));

            if (resolved is T result)
                return result;

            throw new InvalidOperationException(
                $"Failed to cast resolved instance of type {resolved?.GetType().Name ?? "null"} to requested type {typeof(T).Name}");
        }

        /// <summary>
        /// Resolves an instance of the specified type by checking the cache first,
        /// then creating a new instance if not found.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <returns>An instance of the specified type.</returns>
        private object Resolve(Type type)
        {
            // Check if we have a cached singleton instance (fast path, no lock)
            if (_singletons.TryGetValue(type, out var cached))
            {
                return cached;
            }

            // Determine lifetime from type metadata (recursive, no instance needed)
            var lifetime = _lifetimeResolver.DetermineLifetime(type);

            if (lifetime == Lifetime.Singleton)
            {
                lock (_singletonLock)
                {
                    // Double-check after acquiring lock
                    if (_singletons.TryGetValue(type, out cached))
                    {
                        return cached;
                    }

                    var instance = _instanceFactory.CreateInstance(type);
                    _singletons[type] = instance;
                    return instance;
                }
            }

            // Transient - no locking needed, create new instance each time
            return _instanceFactory.CreateInstance(type);
        }
    }
}
