using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyA
{
    /// <summary>
    /// Helper class to resolve ISharedService from Assembly A's context.
    /// The call to Resolve&lt;T&gt;() happens within this assembly, so MagicDI
    /// uses this assembly as the requesting context.
    /// </summary>
    public static class ResolverHelperA
    {
        public static ISharedService ResolveSharedService(MagicDI di)
        {
            // This call originates from AssemblyA, so AssemblyA is searched first
            return di.Resolve<ISharedService>();
        }

        public static ConsumerA ResolveConsumer(MagicDI di)
        {
            return di.Resolve<ConsumerA>();
        }
    }
}
