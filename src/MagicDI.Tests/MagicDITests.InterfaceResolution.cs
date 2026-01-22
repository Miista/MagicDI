using System;
using FluentAssertions;
using Xunit;

// ReSharper disable ClassNeverInstantiated.Global

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class InterfaceResolution
        {
            public class BasicResolution
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
            }

            public class DependencyInjection
            {
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
            }

            public class ErrorHandling
            {
                [Fact]
                public void Throws_when_no_implementation_exists()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<IUnimplementedInterface>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>(because: "there is no implementation for the interface")
                        .WithMessage("*No implementation found*", because: "the error message should explain the problem")
                        .WithMessage("*IUnimplementedInterface*", because: "the error message should include the interface type name")
                        .WithMessage("*Ensure*concrete class*exists*", because: "the error message should provide guidance");
                }

                [Fact]
                public void No_implementation_exception_includes_interface_type_name()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<IUnimplementedInterface>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage("*IUnimplementedInterface*", because: "the error message should include the interface type name to help developers identify which interface lacks an implementation");
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
                        .WithMessage("*Multiple implementations*", because: "the error message should explain the ambiguity")
                        .WithMessage("*IMultipleImplementations*", because: "the error message should include the interface type name")
                        .WithMessage("*ambiguous*", because: "the error message should clarify this is an ambiguity issue");
                }

                [Fact]
                public void Multiple_implementations_exception_lists_competing_types()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<IMultipleImplementations>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage("*ImplementationA*", because: "the error message should list the first competing implementation")
                        .WithMessage("*ImplementationB*", because: "the error message should list all competing implementations to help developers resolve the ambiguity");
                }
            }

            public class LifetimeDetermination
            {
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
            }

            public class SingletonSharing
            {
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

                [Fact]
                public void Shares_singleton_implementation_across_abstract_class_resolutions()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var fromAbstract = di.Resolve<AbstractService>();
                    var fromConcrete = di.Resolve<ConcreteService>();

                    // Assert
                    fromAbstract.Should().BeSameAs(fromConcrete, because: "the same singleton instance should be returned regardless of how it's requested");
                }
            }

            public class AbstractClassResolution
            {
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
                public void Throws_when_no_abstract_implementation_exists()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<AbstractServiceWithNoImplementation>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>(because: "there is no implementation for the abstract class")
                        .WithMessage("*No implementation found*", because: "the error message should explain the problem");
                }

                [Fact]
                public void Throws_when_multiple_implementations_of_abstract_class_exist()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<AbstractServiceWithMultipleImpls>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>(because: "multiple implementations create ambiguity")
                        .WithMessage("*Multiple implementations*", because: "the error message should explain the ambiguity");
                }

                [Fact]
                public void Resolves_abstract_class_dependency_in_constructor()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithAbstractDependency>();

                    // Assert
                    instance.Should().NotBeNull(because: "the container should create the class");
                    instance.Repository.Should().NotBeNull(because: "the container should resolve abstract class dependencies");
                    instance.Repository.Should().BeOfType<ConcreteRepository>(because: "the abstract class should resolve to its implementation");
                }

                [Fact]
                public void Resolves_nested_abstract_class_dependencies()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithNestedAbstractDependency>();

                    // Assert
                    instance.Should().NotBeNull(because: "the container should create the top-level class");
                    instance.Wrapper.Should().NotBeNull(because: "the container should resolve the wrapper");
                    instance.Wrapper.Repository.Should().NotBeNull(because: "the container should resolve nested abstract class dependencies");
                }

                [Fact]
                public void Determines_lifetime_from_abstract_class_implementation()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<AbstractService>();
                    var instance2 = di.Resolve<AbstractService>();

                    // Assert
                    instance1.Should().BeSameAs(instance2, because: "the implementation is a singleton (no transient markers)");
                }

                [Fact]
                public void Disposable_implementation_of_abstract_class_is_transient()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<DisposableAbstractService>();
                    var instance2 = di.Resolve<DisposableAbstractService>();

                    // Assert
                    instance1.Should().NotBeSameAs(instance2, because: "IDisposable implementations should be transient");
                }
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

            public class ClassWithInterfaceDependency(ISimpleService service)
            {
                public ISimpleService Service { get; } = service;
            }

            public class ClassWithNestedInterfaceDependency(ClassWithInterfaceDependency wrapper)
            {
                public ClassWithInterfaceDependency Wrapper { get; } = wrapper;
            }

            // === Abstract class test helpers ===

            // Abstract class with no implementations (for error testing)
            public abstract class AbstractServiceWithNoImplementation
            {
                public abstract void DoWork();
            }

            // Abstract class with multiple implementations (for ambiguity error testing)
            public abstract class AbstractServiceWithMultipleImpls
            {
                public abstract void DoWork();
            }

            public class ConcreteServiceImplA : AbstractServiceWithMultipleImpls
            {
                public override void DoWork() { }
            }

            public class ConcreteServiceImplB : AbstractServiceWithMultipleImpls
            {
                public override void DoWork() { }
            }

            // Abstract class for dependency injection testing
            public abstract class AbstractRepository
            {
                public abstract string GetData();
            }

            public class ConcreteRepository : AbstractRepository
            {
                public override string GetData() => "data";
            }

            // Class that depends on an abstract class
            public class ClassWithAbstractDependency(AbstractRepository repository)
            {
                public AbstractRepository Repository { get; } = repository;
            }

            // Class with nested abstract class dependency
            public class ClassWithNestedAbstractDependency(ClassWithAbstractDependency wrapper)
            {
                public ClassWithAbstractDependency Wrapper { get; } = wrapper;
            }

            // Disposable abstract class for lifetime testing
            public abstract class DisposableAbstractService
            {
                public abstract void DoWork();
            }

            public class DisposableConcreteService : DisposableAbstractService, IDisposable
            {
                public override void DoWork() { }
                public void Dispose() { }
            }

            #endregion
        }
    }
}
