using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyA
{
    /// <summary>
    /// A consumer in Assembly A that depends on ISharedService.
    /// When resolved, should get SharedServiceA due to assembly proximity.
    /// </summary>
    public class ConsumerA(ISharedService service)
    {
        public ISharedService Service { get; } = service;
    }
}
