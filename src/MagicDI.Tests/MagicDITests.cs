using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class General
        {
            [Fact]
            public void ResolvesSimpleTypeSuccessfully()
            {
                var di = new MagicDI();

                var instance = di.Resolve<SimpleClass>();

                instance.Should().NotBeNull("the container should create instances of simple types without dependencies");
            }

            [Fact]
            public void ResolvesDependenciesAutomatically()
            {
                var di = new MagicDI();

                var instance = di.Resolve<ClassWithDependency>();

                instance.Should().NotBeNull("the container should create the requested type");
                instance.Dependency.Should().NotBeNull("the container should automatically resolve and inject constructor dependencies");
            }

            [Fact]
            public void ReturnsSameInstanceForSingletonLifetime()
            {
                var di = new MagicDI();

                var instance1 = di.Resolve<SimpleClass>();
                var instance2 = di.Resolve<SimpleClass>();

                instance1.Should().BeSameAs(instance2, "singleton lifetime means the same instance is returned for all resolutions");
            }

            [Fact]
            public void ResolvesNestedDependenciesRecursively()
            {
                var di = new MagicDI();

                var instance = di.Resolve<ClassWithNestedDependency>();

                instance.Should().NotBeNull("the container should create the top-level type");
                instance.Dependency.Should().NotBeNull("the container should resolve first-level dependencies");
                instance.Dependency.Dependency.Should().NotBeNull("the container should recursively resolve nested dependencies");
            }

            [Fact]
            public void ResolvesAllConstructorParameters()
            {
                var di = new MagicDI();

                var instance = di.Resolve<ClassWithMultipleDependencies>();

                instance.Should().NotBeNull("the container should create types with multiple constructor parameters");
                instance.Dependency1.Should().NotBeNull("the container should resolve the first constructor parameter");
                instance.Dependency2.Should().NotBeNull("the container should resolve the second constructor parameter");
            }

            [Fact]
            public void SharesSingletonInstancesAcrossDependencyGraphs()
            {
                var di = new MagicDI();

                var instance1 = di.Resolve<ClassWithDependency>();
                var instance2 = di.Resolve<ClassWithNestedDependency>();

                instance1.Dependency.Should().BeSameAs(instance2.Dependency.Dependency,
                    "singleton instances should be shared across different dependency graphs");
            }

            [Fact]
            public void ThrowsWhenResolvingPrimitiveTypes()
            {
                var di = new MagicDI();

                var act = () => di.Resolve<int>();

                act.Should().Throw<InvalidOperationException>("primitive types cannot be instantiated by the container");
            }

            [Fact]
            public void SelectsConstructorWithMostParameters()
            {
                var di = new MagicDI();

                var instance = di.Resolve<ClassWithMultipleConstructors>();

                instance.Should().NotBeNull("the container should create the instance");
                instance.Dependency1.Should().NotBeNull("the first dependency should be resolved");
                instance.Dependency2.Should().NotBeNull("the second dependency should be resolved when using the larger constructor");
                instance.UsedLargerConstructor.Should().BeTrue("the container should prefer constructors with more parameters to maximize dependency injection");
            }

            [Fact]
            public void ThrowsWhenTypeHasNoPublicConstructors()
            {
                var di = new MagicDI();

                var act = () => di.Resolve<ClassWithNoPublicConstructor>();

                act.Should().Throw<InvalidOperationException>("types without public constructors cannot be instantiated")
                    .WithMessage("*no public constructors*", "the error message should explain why resolution failed");
            }

            [Fact]
            public void PropagatesExceptionsThrownByConstructors()
            {
                var di = new MagicDI();

                var act = () => di.Resolve<ClassWithThrowingConstructor>();

                act.Should().Throw<TargetInvocationException>("exceptions thrown during construction should propagate to the caller");
            }

            [Fact]
            public void ResolvesDeepDependencyChains()
            {
                var di = new MagicDI();

                var instance = di.Resolve<DeepLevel5>();

                instance.Should().NotBeNull("the container should handle deep dependency chains");
                instance.Level4.Should().NotBeNull("level 4 dependency should be resolved");
                instance.Level4.Level3.Should().NotBeNull("level 3 dependency should be resolved");
                instance.Level4.Level3.Level2.Should().NotBeNull("level 2 dependency should be resolved");
                instance.Level4.Level3.Level2.Level1.Should().NotBeNull("level 1 dependency should be resolved at the bottom of the chain");
            }

            [Fact]
            public void SelectsConstructorDeterministicallyWhenParameterCountsMatch()
            {
                var di = new MagicDI();

                var instance1 = di.Resolve<ClassWithSameParameterCountConstructors>();
                var instance2 = di.Resolve<ClassWithSameParameterCountConstructors>();

                instance1.Should().NotBeNull("first resolution should succeed");
                instance2.Should().NotBeNull("second resolution should succeed with consistent constructor selection");
            }

            [Fact]
            public void ReturnsCorrectTypeFromGenericResolve()
            {
                var di = new MagicDI();

                var instance = di.Resolve<SimpleClass>();

                instance.Should().NotBeNull("resolution should succeed");
                instance.Should().BeOfType<SimpleClass>("the generic Resolve<T> method should return the exact requested type");
            }

            #region Test Classes

            public class SimpleClass { }

            public class ClassWithDependency
            {
                public SimpleClass Dependency { get; }

                public ClassWithDependency(SimpleClass dependency)
                {
                    Dependency = dependency;
                }
            }

            public class ClassWithNestedDependency
            {
                public ClassWithDependency Dependency { get; }

                public ClassWithNestedDependency(ClassWithDependency dependency)
                {
                    Dependency = dependency;
                }
            }

            public class ClassWithMultipleDependencies
            {
                public SimpleClass Dependency1 { get; }
                public ClassWithDependency Dependency2 { get; }

                public ClassWithMultipleDependencies(
                    SimpleClass dependency1,
                    ClassWithDependency dependency2
                )
                {
                    Dependency1 = dependency1;
                    Dependency2 = dependency2;
                }
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

            public class DeepLevel1 { }

            public class DeepLevel2
            {
                public DeepLevel1 Level1 { get; }
                public DeepLevel2(DeepLevel1 level1) => Level1 = level1;
            }

            public class DeepLevel3
            {
                public DeepLevel2 Level2 { get; }
                public DeepLevel3(DeepLevel2 level2) => Level2 = level2;
            }

            public class DeepLevel4
            {
                public DeepLevel3 Level3 { get; }
                public DeepLevel4(DeepLevel3 level3) => Level3 = level3;
            }

            public class DeepLevel5
            {
                public DeepLevel4 Level4 { get; }
                public DeepLevel5(DeepLevel4 level4) => Level4 = level4;
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
