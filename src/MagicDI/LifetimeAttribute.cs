using System;

namespace MagicDI
{
    /// <summary>
    /// Specifies an explicit lifetime for a class, overriding automatic inference.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class LifetimeAttribute : Attribute
    {
        /// <summary>
        /// Gets the specified lifetime for the class.
        /// </summary>
        public Lifetime Lifetime { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LifetimeAttribute"/> class.
        /// </summary>
        /// <param name="lifetime">The lifetime to use for this class.</param>
        public LifetimeAttribute(Lifetime lifetime)
        {
            Lifetime = lifetime;
        }
    }
}
