using FluentAssertions;
using MagicDI.Tests.AssemblyA;
using MagicDI.Tests.AssemblyB;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class ContextAwareResolution
        {
            public class DirectResolution
            {
                [Fact]
                public void Resolves_from_assembly_A_when_called_from_assembly_A()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - ResolverHelperA.ResolveSharedService calls di.Resolve<ISharedService>() from AssemblyA
                    var service = ResolverHelperA.ResolveSharedService(di);

                    // Assert
                    service.GetAssemblyName().Should().Be("AssemblyA",
                        because: "the Resolve call originated from AssemblyA, so its implementation should be found first");
                }

                [Fact]
                public void Resolves_from_assembly_B_when_called_from_assembly_B()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - ResolverHelperB.ResolveSharedService calls di.Resolve<ISharedService>() from AssemblyB
                    var service = ResolverHelperB.ResolveSharedService(di);

                    // Assert
                    service.GetAssemblyName().Should().Be("AssemblyB",
                        because: "the Resolve call originated from AssemblyB, so its implementation should be found first");
                }
            }

            public class NestedDependencyResolution
            {
                [Fact]
                public void Consumer_in_assembly_A_gets_implementation_from_assembly_A()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var consumer = di.Resolve<ConsumerA>();

                    // Assert
                    consumer.Service.GetAssemblyName().Should().Be("AssemblyA",
                        because: "ConsumerA is in AssemblyA, so its ISharedService dependency should resolve to SharedServiceA");
                }

                [Fact]
                public void Consumer_in_assembly_B_gets_implementation_from_assembly_B()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var consumer = di.Resolve<ConsumerB>();

                    // Assert
                    consumer.Service.GetAssemblyName().Should().Be("AssemblyB",
                        because: "ConsumerB is in AssemblyB, so its ISharedService dependency should resolve to SharedServiceB");
                }
            }

            public class SameContainerDifferentContexts
            {
                [Fact]
                public void Same_container_resolves_different_implementations_based_on_context()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var consumerA = di.Resolve<ConsumerA>();
                    var consumerB = di.Resolve<ConsumerB>();

                    // Assert
                    consumerA.Service.Should().NotBeSameAs(consumerB.Service,
                        because: "each consumer gets its own assembly's implementation");

                    consumerA.Service.GetAssemblyName().Should().Be("AssemblyA");
                    consumerB.Service.GetAssemblyName().Should().Be("AssemblyB");
                }

                [Fact]
                public void Context_switches_correctly_between_resolve_calls()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - Alternate between contexts
                    var serviceA1 = ResolverHelperA.ResolveSharedService(di);
                    var serviceB1 = ResolverHelperB.ResolveSharedService(di);
                    var serviceA2 = ResolverHelperA.ResolveSharedService(di);
                    var serviceB2 = ResolverHelperB.ResolveSharedService(di);

                    // Assert
                    serviceA1.GetAssemblyName().Should().Be("AssemblyA");
                    serviceB1.GetAssemblyName().Should().Be("AssemblyB");
                    serviceA2.GetAssemblyName().Should().Be("AssemblyA");
                    serviceB2.GetAssemblyName().Should().Be("AssemblyB");
                }
            }

            public class SingletonBehaviorWithContext
            {
                [Fact]
                public void Singleton_implementation_is_shared_within_same_context()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var service1 = ResolverHelperA.ResolveSharedService(di);
                    var service2 = ResolverHelperA.ResolveSharedService(di);

                    // Assert
                    service1.Should().BeSameAs(service2,
                        because: "singleton instances should be cached and reused within the same context");
                }

                [Fact]
                public void Consumer_and_direct_resolution_share_singleton_in_same_context()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var consumer = di.Resolve<ConsumerA>();
                    var directService = ResolverHelperA.ResolveSharedService(di);

                    // Assert
                    consumer.Service.Should().BeSameAs(directService,
                        because: "the same concrete type resolved in the same context should be the same singleton");
                }
            }
        }
    }
}
