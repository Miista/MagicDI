using System;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class InterfaceResolution
        {
            [Fact]
            public void Resolves_interface_to_single_implementation()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance = di.Resolve<ISimpleService>();

                // Assert
                instance.Should().NotBeNull(because: "the container should find and create the single implementation");
                instance.Should().BeOfType<SimpleService>(because: "the container should resolve to the concrete implementation");
            }

            [Fact]
            public void Resolves_interface_dependency_in_constructor()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance = di.Resolve<ClassWithInterfaceDependency>();

                // Assert
                instance.Should().NotBeNull(because: "the container should create the class");
                instance.Service.Should().NotBeNull(because: "the container should resolve interface dependencies");
                instance.Service.Should().BeOfType<SimpleService>(because: "the interface should resolve to its implementation");
            }

            [Fact]
            public void Resolves_nested_interface_dependencies()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance = di.Resolve<ClassWithNestedInterfaceDependency>();

                // Assert
                instance.Should().NotBeNull(because: "the container should create the top-level class");
                instance.Wrapper.Should().NotBeNull(because: "the container should resolve the wrapper");
                instance.Wrapper.Service.Should().NotBeNull(because: "the container should resolve nested interface dependencies");
            }

            [Fact]
            public void Throws_when_no_implementation_exists()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action act = () => di.Resolve<IUnimplementedInterface>();

                // Assert
                act.Should().Throw<InvalidOperationException>(because: "there is no implementation for the interface")
                    .WithMessage("*No implementation found*", because: "the error message should explain the problem");
            }

            [Fact]
            public void Throws_when_multiple_implementations_exist()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action act = () => di.Resolve<IMultipleImplementations>();

                // Assert
                act.Should().Throw<InvalidOperationException>(because: "multiple implementations create ambiguity")
                    .WithMessage("*Multiple implementations*", because: "the error message should explain the ambiguity");
            }

            [Fact]
            public void Determines_lifetime_from_implementation()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<ISimpleService>();
                var instance2 = di.Resolve<ISimpleService>();

                // Assert
                instance1.Should().BeSameAs(instance2, because: "the implementation is a singleton (no transient markers)");
            }

            [Fact]
            public void Disposable_implementation_is_transient()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<IDisposableService>();
                var instance2 = di.Resolve<IDisposableService>();

                // Assert
                instance1.Should().NotBeSameAs(instance2, because: "IDisposable implementations should be transient");
            }

            [Fact]
            public void Resolves_abstract_class_to_implementation()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance = di.Resolve<AbstractService>();

                // Assert
                instance.Should().NotBeNull(because: "the container should find the concrete implementation");
                instance.Should().BeOfType<ConcreteService>(because: "the abstract class should resolve to its implementation");
            }

            [Fact]
            public void Shares_singleton_implementation_across_interface_resolutions()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var fromInterface = di.Resolve<ISimpleService>();
                var fromConcrete = di.Resolve<SimpleService>();

                // Assert
                fromInterface.Should().BeSameAs(fromConcrete, because: "the same singleton instance should be returned regardless of how it's requested");
            }

            #region Test Interfaces and Classes

            public interface ISimpleService
            {
                void DoWork();
            }

            public class SimpleService : ISimpleService
            {
                public void DoWork() { }
            }

            public interface IUnimplementedInterface
            {
                void NeverImplemented();
            }

            public interface IMultipleImplementations
            {
                void DoSomething();
            }

            public class ImplementationA : IMultipleImplementations
            {
                public void DoSomething() { }
            }

            public class ImplementationB : IMultipleImplementations
            {
                public void DoSomething() { }
            }

            public interface IDisposableService
            {
                void DoWork();
            }

            public class DisposableService : IDisposableService, IDisposable
            {
                public void DoWork() { }
                public void Dispose() { }
            }

            public abstract class AbstractService
            {
                public abstract void DoWork();
            }

            public class ConcreteService : AbstractService
            {
                public override void DoWork() { }
            }

            public class ClassWithInterfaceDependency
            {
                public ISimpleService Service { get; }

                public ClassWithInterfaceDependency(ISimpleService service)
                {
                    Service = service;
                }
            }

            public class ClassWithNestedInterfaceDependency
            {
                public ClassWithInterfaceDependency Wrapper { get; }

                public ClassWithNestedInterfaceDependency(ClassWithInterfaceDependency wrapper)
                {
                    Wrapper = wrapper;
                }
            }

            #endregion
        }
    }
}
