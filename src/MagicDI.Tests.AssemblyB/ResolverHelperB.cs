using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyB
{
    /// <summary>
    /// Helper class to resolve ISharedService from Assembly B's context.
    /// </summary>
    public static class ResolverHelperB
    {
        public static string AssemblyName => typeof(ResolverHelperB).Assembly.GetName().Name;
        
        public static ISharedService ResolveSharedService(MagicDI di)
        {
            // This call originates from AssemblyB, so AssemblyB is searched first
            return di.Resolve<ISharedService>();
        }

        public static ConsumerB ResolveConsumer(MagicDI di)
        {
            return di.Resolve<ConsumerB>();
        }
    }
}
