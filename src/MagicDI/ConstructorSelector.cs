using System;
using System.Linq;
using System.Reflection;

namespace MagicDI
{
    /// <summary>
    /// Selects the most appropriate constructor for a type.
    /// </summary>
    internal static class ConstructorSelector
    {
        /// <summary>
        /// Gets the most appropriate constructor for the specified type.
        /// Selects the constructor with the most parameters, using metadata token order as a tiebreaker.
        /// </summary>
        /// <param name="type">The type to get the constructor for.</param>
        /// <returns>The selected constructor.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the type has no public constructors.</exception>
        public static ConstructorInfo GetConstructor(Type type)
        {
            var constructors = type.GetConstructors();

            // Check if any constructor has ref/out parameters
            var hasRefOut = constructors.Any(c =>
                c.GetParameters().Any(p => p.ParameterType.IsByRef));

            if (hasRefOut)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because its constructor has ref or out parameters");

            var appropriateConstructor = constructors
                .OrderByDescending(info => info.GetParameters().Length)
                .ThenBy(info => info.MetadataToken)
                .FirstOrDefault();

            if (appropriateConstructor == null)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because it has no public constructors");

            return appropriateConstructor;
        }
    }
}
