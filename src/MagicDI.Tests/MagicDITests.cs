using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class General
        {
            public class BasicResolution
            {
                [Fact]
                public void Resolves_simple_type_successfully()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<SimpleClass>();

                    // Assert
                    instance.Should().NotBeNull(because: "the container should create instances of simple types without dependencies");
                }

                [Fact]
                public void Returns_correct_type_from_generic_resolve()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<SimpleClass>();

                    // Assert
                    instance.Should().NotBeNull(because: "resolution should succeed");
                    instance.Should().BeOfType<SimpleClass>(because: "the generic Resolve<T> method should return the exact requested type");
                }
            }

            public class DependencyInjection
            {
                [Fact]
                public void Resolves_dependencies_automatically()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithDependency>();

                    // Assert
                    instance.Should().NotBeNull(because: "the container should create the requested type");
                    instance.Dependency.Should().NotBeNull(because: "the container should automatically resolve and inject constructor dependencies");
                }

                [Fact]
                public void Resolves_nested_dependencies_recursively()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithNestedDependency>();

                    // Assert
                    instance.Should().NotBeNull(because: "the container should create the top-level type");
                    instance.Dependency.Should().NotBeNull(because: "the container should resolve first-level dependencies");
                    instance.Dependency.Dependency.Should().NotBeNull(because: "the container should recursively resolve nested dependencies");
                }

                [Fact]
                public void Resolves_all_constructor_parameters()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithMultipleDependencies>();

                    // Assert
                    instance.Should().NotBeNull(because: "the container should create types with multiple constructor parameters");
                    instance.Dependency1.Should().NotBeNull(because: "the container should resolve the first constructor parameter");
                    instance.Dependency2.Should().NotBeNull(because: "the container should resolve the second constructor parameter");
                }

                [Fact]
                public void Resolves_deep_dependency_chains()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<DeepLevel5>();

                    // Assert
                    instance.Should().NotBeNull(because: "the container should handle deep dependency chains");
                    instance.Level4.Should().NotBeNull(because: "level 4 dependency should be resolved");
                    instance.Level4.Level3.Should().NotBeNull(because: "level 3 dependency should be resolved");
                    instance.Level4.Level3.Level2.Should().NotBeNull(because: "level 2 dependency should be resolved");
                    instance.Level4.Level3.Level2.Level1.Should().NotBeNull(because: "level 1 dependency should be resolved at the bottom of the chain");
                }
            }

            public class ConstructorSelection
            {
                [Fact]
                public void Selects_constructor_with_most_parameters()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithMultipleConstructors>();

                    // Assert
                    instance.Should().NotBeNull(because: "the container should create the instance");
                    instance.Dependency1.Should().NotBeNull(because: "the first dependency should be resolved");
                    instance.Dependency2.Should().NotBeNull(because: "the second dependency should be resolved when using the larger constructor");
                    instance.UsedLargerConstructor.Should().BeTrue(because: "the container should prefer constructors with more parameters to maximize dependency injection");
                }

                [Fact]
                public void Selects_constructor_deterministically_when_parameter_counts_match()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<ClassWithSameParameterCountConstructors>();
                    var instance2 = di.Resolve<ClassWithSameParameterCountConstructors>();

                    // Assert
                    instance1.Should().NotBeNull(because: "first resolution should succeed");
                    instance2.Should().NotBeNull(because: "second resolution should succeed with consistent constructor selection");
                }
            }

            public class ErrorHandling
            {
                [Fact]
                public void Throws_when_resolving_primitive_types()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<int>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>(because: "primitive types cannot be instantiated by the container");
                }

                [Fact]
                public void Throws_when_type_has_no_public_constructors()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<ClassWithNoPublicConstructor>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>(because: "types without public constructors cannot be instantiated")
                        .WithMessage("*no public constructors*", because: "the error message should explain why resolution failed");
                }

                [Fact]
                public void Propagates_exceptions_thrown_by_constructors()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<ClassWithThrowingConstructor>();

                    // Assert
                    act.Should().Throw<TargetInvocationException>(because: "exceptions thrown during construction should propagate to the caller");
                }
            }

            #region Test Classes

            public class SimpleClass;

            public class ClassWithDependency(SimpleClass dependency)
            {
                public SimpleClass Dependency { get; } = dependency;
            }

            public class ClassWithNestedDependency(ClassWithDependency dependency)
            {
                public ClassWithDependency Dependency { get; } = dependency;
            }

            public class ClassWithMultipleDependencies(
                SimpleClass dependency1,
                ClassWithDependency dependency2
            )
            {
                public SimpleClass Dependency1 { get; } = dependency1;
                public ClassWithDependency Dependency2 { get; } = dependency2;
            }

            public class ClassWithMultipleConstructors
            {
                public SimpleClass Dependency1 { get; }
                public ClassWithDependency Dependency2 { get; }
                public bool UsedLargerConstructor { get; }

                public ClassWithMultipleConstructors(SimpleClass dependency1)
                {
                    Dependency1 = dependency1;
                    UsedLargerConstructor = false;
                }

                public ClassWithMultipleConstructors(
                    SimpleClass dependency1,
                    ClassWithDependency dependency2
                )
                {
                    Dependency1 = dependency1;
                    Dependency2 = dependency2;
                    UsedLargerConstructor = true;
                }
            }

            public class ClassWithNoPublicConstructor
            {
                private ClassWithNoPublicConstructor() { }
            }

            public class ClassWithThrowingConstructor
            {
                public ClassWithThrowingConstructor()
                {
                    throw new InvalidOperationException("Constructor intentionally throws");
                }
            }

            public class DeepLevel1;

            public class DeepLevel2(DeepLevel1 level1)
            {
                public DeepLevel1 Level1 { get; } = level1;
            }

            public class DeepLevel3(DeepLevel2 level2)
            {
                public DeepLevel2 Level2 { get; } = level2;
            }

            public class DeepLevel4(DeepLevel3 level3)
            {
                public DeepLevel3 Level3 { get; } = level3;
            }

            public class DeepLevel5(DeepLevel4 level4)
            {
                public DeepLevel4 Level4 { get; } = level4;
            }

            public class ClassWithSameParameterCountConstructors
            {
                public SimpleClass Dependency { get; }

                public ClassWithSameParameterCountConstructors(SimpleClass dependency)
                {
                    Dependency = dependency;
                }

                public ClassWithSameParameterCountConstructors(ClassWithDependency dependency)
                {
                    // Different parameter type, same count
                    Dependency = dependency?.Dependency;
                }
            }

            #endregion
        }
    }
}
