namespace MagicDI.Tests.Contracts
{
    /// <summary>
    /// Interface for testing context-aware resolution.
    /// Different assemblies will provide different implementations.
    /// </summary>
    public interface ISharedService
    {
        string GetAssemblyName();
    }
}
