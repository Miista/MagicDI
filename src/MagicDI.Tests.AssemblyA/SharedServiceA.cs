using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyA
{
    /// <summary>
    /// Assembly A's implementation of ISharedService.
    /// </summary>
    public class SharedServiceA : ISharedService
    {
        public string GetAssemblyName() => GetType().Assembly.GetName().Name;
    }
}
