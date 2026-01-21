using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MagicDI
{
    /// <summary>
    /// A lightweight dependency injection container that uses reflection
    /// to automatically resolve constructor dependencies.
    /// </summary>
    public class MagicDI
    {
        private readonly Dictionary<Type, InstanceRegistry> _registeredInstances = new();

        /// <summary>
        /// Tracks the determined lifetime for all resolved types.
        /// This is separate from _registeredInstances because transients are not cached there.
        /// </summary>
        private readonly Dictionary<Type, Lifetime> _lifetimes = new();

        /// <summary>
        /// Tracks types currently being resolved on each thread to detect circular dependencies.
        /// Using ThreadLocal ensures thread-safety for the resolution stack.
        /// </summary>
        private readonly ThreadLocal<HashSet<Type>> _resolutionStack = new(() => []);

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
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an unknown lifetime is encountered.</exception>
        private object Resolve(Type type)
        {
            // 0. Check if we have a cached singleton instance
            if (_registeredInstances.TryGetValue(type, out var instanceRegistry))
            {
                return instanceRegistry.Value;
            }

            var resolvedInstance = ResolveInstance(type);

            // 4. Determine lifetime (Scoped, Transient, Singleton)
            var lifetime = DetermineLifeTime(resolvedInstance);

            // 5. Track lifetime for all types (needed for captive dependency detection)
            _lifetimes[type] = lifetime;

            // 6. Only cache singletons - transients should create new instances each time
            if (lifetime == Lifetime.Singleton)
            {
                var registry = new InstanceRegistry { Type = type, Lifetime = lifetime, Value = resolvedInstance };
                _registeredInstances.Add(type, registry);
            }

            // 7. Return created instance
            return resolvedInstance;
        }

        /// <summary>
        /// Creates a new instance of the specified type by finding and invoking the appropriate constructor.
        /// </summary>
        /// <param name="type">The type to instantiate.</param>
        /// <returns>A new instance of the specified type.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the type is a primitive type, when a circular dependency is detected,
        /// or when constructor invocation returns null.
        /// </exception>
        private object ResolveInstance(Type type)
        {
            if (type.IsPrimitive)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because it is a primitive type");

            // Check for circular dependency
            if (!_resolutionStack.Value.Add(type))
            {
                var chain = string.Join(" -> ", _resolutionStack.Value.Select(t => t.Name)) + " -> " + type.Name;
                throw new InvalidOperationException(
                    $"Circular dependency detected while resolving {type.Name}. Resolution chain: {chain}");
            }

            try
            {
                // 1. Find the most appropriate constructor
                var constructorInfo = GetConstructor(type);

                // 2. Resolve arguments to said constructor
                var resolvedConstructorArguments = ResolveConstructorArguments(constructorInfo);

                // 3. Invoke constructor
                var instance = constructorInfo.Invoke(resolvedConstructorArguments);

                if (instance == null)
                    throw new InvalidOperationException(
                        $"Constructor invocation for type {type.Name} returned null");

                return instance;
            }
            finally
            {
                // Always remove the type from the resolution stack when done
                _resolutionStack.Value.Remove(type);
            }
        }

        /// <summary>
        /// Gets the most appropriate constructor for the specified type.
        /// Selects the constructor with the most parameters, using metadata token order as a tiebreaker.
        /// </summary>
        /// <param name="type">The type to get the constructor for.</param>
        /// <returns>The selected constructor.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the type has no public constructors.</exception>
        private static ConstructorInfo GetConstructor(Type type)
        {
            var appropriateConstructor = type.GetConstructors()
                .OrderByDescending(info => info.GetParameters().Length)
                .ThenBy(info => info.MetadataToken)
                .FirstOrDefault();

            if (appropriateConstructor == null)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because it has no public constructors");

            return appropriateConstructor;
        }

        /// <summary>
        /// Resolves all constructor parameters by recursively resolving each parameter type.
        /// </summary>
        /// <param name="constructorInfo">The constructor whose parameters should be resolved.</param>
        /// <returns>An array of resolved parameter instances.</returns>
        private object[] ResolveConstructorArguments(ConstructorInfo constructorInfo)
        {
            return constructorInfo
                .GetParameters()
                .Select(info => info.ParameterType)
                .Select(Resolve)
                .ToArray();
        }

        /// <summary>
        /// Determines the lifetime for a resolved instance using the following priority:
        /// <list type="number">
        /// <item>Explicit [Lifetime] attribute override (with captive dependency validation)</item>
        /// <item>IDisposable types → Transient</item>
        /// <item>Cascade from dependencies (least cacheable wins)</item>
        /// <item>No dependencies → Singleton</item>
        /// </list>
        /// </summary>
        /// <param name="instance">The resolved instance to determine lifetime for.</param>
        /// <returns>The inferred or explicitly specified lifetime.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a type is explicitly marked as Singleton but depends on a Transient type (captive dependency).
        /// </exception>
        private Lifetime DetermineLifeTime(object instance)
        {
            var type = instance.GetType();
            var attr = type.GetCustomAttribute<LifetimeAttribute>();

            // Find transient dependencies
            var constructor = GetConstructor(type);
            var transientDependency = constructor.GetParameters()
                .Select(p => p.ParameterType)
                .FirstOrDefault(depType =>
                    _lifetimes.TryGetValue(depType, out var depLifetime) &&
                    depLifetime == Lifetime.Transient);

            // 1. Check for explicit attribute override
            if (attr != null)
            {
                // Validate: explicit Singleton with transient dependency is a captive dependency error
                if (attr.Lifetime == Lifetime.Singleton && transientDependency != null)
                {
                    throw new InvalidOperationException(
                        $"Captive dependency detected: Singleton '{type.Name}' depends on Transient '{transientDependency.Name}'. " +
                        $"This would cause the transient instance to be captured and never released. " +
                        $"Either remove the [Lifetime(Singleton)] attribute from '{type.Name}', or mark '{transientDependency.Name}' as Singleton.");
                }

                return attr.Lifetime;
            }

            // 2. IDisposable → Transient
            if (typeof(IDisposable).IsAssignableFrom(type))
                return Lifetime.Transient;

            // 3. Cascade from dependencies - use the least cacheable lifetime
            if (transientDependency != null)
                return Lifetime.Transient;

            // 4. No deps or all deps Singleton → Singleton
            return Lifetime.Singleton;
        }
    }
}
