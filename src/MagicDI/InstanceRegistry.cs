using System;

namespace MagicDI
{
    /// <summary>
    /// Internal class for tracking resolved instances and their lifetimes.
    /// </summary>
    internal class InstanceRegistry
    {
        /// <summary>
        /// Gets or sets the type of the registered instance.
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Gets or sets the lifetime of the registered instance.
        /// </summary>
        public Lifetime Lifetime { get; set; }

        /// <summary>
        /// Gets or sets the resolved instance value.
        /// </summary>
        public object Value { get; set; }
    }
}
