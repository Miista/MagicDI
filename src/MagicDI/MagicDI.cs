using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

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
        /// Tracks the current requesting type context for interface resolution.
        /// Each nested Resolve call pushes its concrete type onto the stack,
        /// so dependencies know who is requesting them.
        /// </summary>
        private readonly ThreadLocal<Stack<Type>> _contextStack = new(() => new());

        /// <summary>
        /// Initializes a new instance of the <see cref="MagicDI"/> class.
        /// </summary>
        public MagicDI()
        {
            _instanceFactory = new InstanceFactory(ResolveWithContext);
        }

        /// <summary>
        /// Resolves an instance of the specified type, automatically
        /// resolving all constructor dependencies.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <returns>An instance of the specified type.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public T Resolve<T>()
        {
            var callingType = GetCallingType();
            var resolved = Resolve(typeof(T), callingType);

            if (resolved is T result)
                return result;

            throw new InvalidOperationException(
                $"Failed to cast resolved instance of type {resolved.GetType().Name} to requested type {typeof(T).Name}");
        }

        /// <summary>
        /// Resolver delegate that reads context from the thread-local stack.
        /// This is passed to InstanceFactory and called when resolving constructor parameters.
        /// </summary>
        private object ResolveWithContext(Type type)
        {
            var context = _contextStack.Value.Count > 0 ? _contextStack.Value.Peek() : null;
            return Resolve(type, context);
        }

        /// <summary>
        /// Resolves an instance of the specified type by checking the cache first,
        /// then creating a new instance if not found.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <param name="requestingType">The type requesting this resolution, used for interface lookup.</param>
        /// <returns>An instance of the specified type.</returns>
        private object Resolve(Type type, Type? requestingType)
        {
            // Reject value types early with a clear error message
            if (type.IsValueType)
            {
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type '{type.Name}' because it is a value type. " +
                    "MagicDI only supports resolving class types.");
            }

            // Resolve interface/abstract to concrete type
            var concreteType = ImplementationFinder.GetConcreteType(type, requestingType);

            // Check if we have a cached singleton instance (fast path, no lock)
            if (_singletons.TryGetValue(concreteType, out var cached))
            {
                return cached;
            }

            // Determine lifetime from type metadata (recursive, no instance needed)
            var lifetime = _lifetimeResolver.DetermineLifetime(concreteType);

            // Push context for nested dependency resolution
            _contextStack.Value.Push(concreteType);
            try
            {
                if (lifetime == Lifetime.Singleton)
                {
                    lock (_singletonLock)
                    {
                        // Double-check after acquiring lock
                        if (_singletons.TryGetValue(concreteType, out cached))
                        {
                            return cached;
                        }

                        var instance = _instanceFactory.CreateInstance(concreteType);
                        _singletons[concreteType] = instance;
                        return instance;
                    }
                }

                // Transient - no locking needed, create new instance each time
                return _instanceFactory.CreateInstance(concreteType);
            }
            finally
            {
                _contextStack.Value.Pop();
            }
        }

        /// <summary>
        /// Gets the type that called into MagicDI by walking the stack trace.
        /// Returns the first type that is not part of the MagicDI assembly.
        /// </summary>
        private static Type? GetCallingType()
        {
            var stackTrace = new StackTrace();
            var frames = stackTrace.GetFrames();

            if (frames == null)
                return null;

            var magicDIAssembly = typeof(MagicDI).Assembly;

            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                var declaringType = method?.DeclaringType;

                if (declaringType != null && declaringType.Assembly != magicDIAssembly)
                {
                    return declaringType;
                }
            }

            return null;
        }
    }
}
