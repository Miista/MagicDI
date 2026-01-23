namespace MagicDI
{
    /// <summary>
    /// Specifies the lifetime of a resolved dependency.
    /// </summary>
    public enum Lifetime
    {
        /// <summary>
        /// A new instance is created every time the dependency is resolved.
        /// </summary>
        Transient,

        /// <summary>
        /// A single instance is shared across all resolutions.
        /// </summary>
        Singleton
    }
}
