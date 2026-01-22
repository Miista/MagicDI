using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyB
{
    /// <summary>
    /// Assembly B's implementation of ISharedService.
    /// </summary>
    public class SharedServiceB : ISharedService
    {
        public string GetAssemblyName() => "AssemblyB";
    }
}
