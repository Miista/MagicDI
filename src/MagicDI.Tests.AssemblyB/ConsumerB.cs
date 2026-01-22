using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyB
{
    /// <summary>
    /// A consumer in Assembly B that depends on ISharedService.
    /// When resolved, should get SharedServiceB due to assembly proximity.
    /// </summary>
    public class ConsumerB(ISharedService service)
    {
        public ISharedService Service { get; } = service;
    }
}
