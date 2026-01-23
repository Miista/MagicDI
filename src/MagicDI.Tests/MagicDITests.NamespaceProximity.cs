using System;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class NamespaceProximity
        {
            [Fact]
            public void Resolves_implementation_in_same_namespace_over_different_namespace()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var consumer = di.Resolve<NamespaceTests.Module1.ConsumerInModule1>();

                // Assert
                consumer.Service.Should().BeOfType<NamespaceTests.Module1.ServiceInModule1>(
                    because: "the implementation in the same namespace should be preferred");
            }

            [Fact]
            public void Resolves_implementation_in_parent_namespace_over_sibling_namespace()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var consumer = di.Resolve<NamespaceTests.Module1.SubModule.ConsumerInSubModule>();

                // Assert
                consumer.Service.Should().BeOfType<NamespaceTests.Module1.ServiceInModule1>(
                    because: "the implementation in the parent namespace (distance 1) should be preferred over sibling namespace (distance 2)");
            }

            [Fact]
            public void Throws_when_multiple_implementations_in_same_namespace()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action act = () => di.Resolve<NamespaceTests.Ambiguous.ConsumerWithAmbiguousService>();

                // Assert
                act.Should().Throw<InvalidOperationException>(
                    because: "multiple implementations at the same distance are ambiguous")
                    .WithMessage("*Multiple implementations*")
                    .WithMessage("*ambiguous*");
            }

            [Fact]
            public void Resolves_closer_namespace_in_deep_hierarchy()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var consumer = di.Resolve<NamespaceTests.Deep.Level1.Level2.Level3.DeepConsumer>();

                // Assert
                consumer.Service.Should().BeOfType<NamespaceTests.Deep.Level1.Level2.ServiceAtLevel2>(
                    because: "Level2 (distance 1) is closer than Level1 (distance 2) or Deep (distance 3)");
            }
        }
    }
}

// Test types in different namespaces to verify namespace proximity resolution

namespace NamespaceTests
{
    public interface IProximityService
    {
        string GetLocation();
    }
}

namespace NamespaceTests.Module1
{
    public class ServiceInModule1 : IProximityService
    {
        public string GetLocation() => "Module1";
    }

    public class ConsumerInModule1(IProximityService service)
    {
        public IProximityService Service { get; } = service;
    }
}

namespace NamespaceTests.Module2
{
    public class ServiceInModule2 : IProximityService
    {
        public string GetLocation() => "Module2";
    }
}

namespace NamespaceTests.Module1.SubModule
{
    // Consumer in SubModule - should get ServiceInModule1 (parent) over ServiceInModule2 (sibling of parent)
    public class ConsumerInSubModule(IProximityService service)
    {
        public IProximityService Service { get; } = service;
    }
}

namespace NamespaceTests.Ambiguous
{
    public interface IAmbiguousService { }

    // Two implementations in the same namespace = truly ambiguous
    public class AmbiguousServiceA : IAmbiguousService { }
    public class AmbiguousServiceB : IAmbiguousService { }

    public class ConsumerWithAmbiguousService(IAmbiguousService service)
    {
        public IAmbiguousService Service { get; } = service;
    }
}

namespace NamespaceTests.Deep
{
    public interface IDeepService { }

    public class ServiceAtDeep : IDeepService { }
}

namespace NamespaceTests.Deep.Level1
{
    public class ServiceAtLevel1 : IDeepService { }
}

namespace NamespaceTests.Deep.Level1.Level2
{
    public class ServiceAtLevel2 : IDeepService { }
}

namespace NamespaceTests.Deep.Level1.Level2.Level3
{
    // Consumer at Level3 should prefer Level2 (distance 1) over Level1 (distance 2) or Deep (distance 3)
    public class DeepConsumer(IDeepService service)
    {
        public IDeepService Service { get; } = service;
    }
}
