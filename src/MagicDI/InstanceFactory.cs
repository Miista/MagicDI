using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MagicDI
{
    /// <summary>
    /// Creates instances of types by resolving constructor dependencies.
    /// <param name="resolver">A delegate that resolves dependencies for constructor parameters.</param>
    /// </summary>
    internal class InstanceFactory(Func<Type, object> resolver)
    {
        /// <summary>
        /// Tracks types currently being resolved on each thread to detect circular dependencies.
        /// Using ThreadLocal ensures thread-safety for the resolution stack.
        /// </summary>
        private readonly ThreadLocal<HashSet<Type>> _resolutionStack = new(() => []);

        /// <summary>
        /// Creates a new instance of the specified type by finding and invoking the appropriate constructor.
        /// </summary>
        /// <param name="type">The type to instantiate.</param>
        /// <returns>A new instance of the specified type.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the type is a primitive type, when a circular dependency is detected,
        /// or when constructor invocation returns null.
        /// </exception>
        public object CreateInstance(Type type)
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
                var constructorInfo = ConstructorSelector.GetConstructor(type);

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
        /// Resolves all constructor parameters by recursively resolving each parameter type.
        /// </summary>
        /// <param name="constructorInfo">The constructor whose parameters should be resolved.</param>
        /// <returns>An array of resolved parameter instances.</returns>
        private object[] ResolveConstructorArguments(ConstructorInfo constructorInfo)
        {
            return constructorInfo
                .GetParameters()
                .Select(info => info.ParameterType)
                .Select(resolver)
                .ToArray();
        }
    }
}
