using System;
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

                Assert.NotNull(instance);
            }

            [Fact]
            public void ResolvesDependenciesAutomatically()
            {
                var di = new MagicDI();

                var instance = di.Resolve<ClassWithDependency>();

                Assert.NotNull(instance);
                Assert.NotNull(instance.Dependency);
            }

            [Fact]
            public void ReturnsSameInstanceForSingletonLifetime()
            {
                var di = new MagicDI();

                var instance1 = di.Resolve<SimpleClass>();
                var instance2 = di.Resolve<SimpleClass>();

                Assert.Same(instance1, instance2);
            }

            [Fact]
            public void ResolvesNestedDependenciesRecursively()
            {
                var di = new MagicDI();

                var instance = di.Resolve<ClassWithNestedDependency>();

                Assert.NotNull(instance);
                Assert.NotNull(instance.Dependency);
                Assert.NotNull(instance.Dependency.Dependency);
            }

            [Fact]
            public void ResolvesAllConstructorParameters()
            {
                var di = new MagicDI();

                var instance = di.Resolve<ClassWithMultipleDependencies>();

                Assert.NotNull(instance);
                Assert.NotNull(instance.Dependency1);
                Assert.NotNull(instance.Dependency2);
            }

            [Fact]
            public void SharesSingletonInstancesAcrossDependencyGraphs()
            {
                var di = new MagicDI();

                var instance1 = di.Resolve<ClassWithDependency>();
                var instance2 = di.Resolve<ClassWithNestedDependency>();

                Assert.Same(instance1.Dependency, instance2.Dependency.Dependency);
            }

            [Fact]
            public void ThrowsWhenResolvingPrimitiveTypes()
            {
                var di = new MagicDI();

                Assert.Throws<InvalidOperationException>(() => di.Resolve<int>());
            }

            [Fact]
            public void SelectsConstructorWithMostParameters()
            {
                var di = new MagicDI();

                var instance = di.Resolve<ClassWithMultipleConstructors>();

                Assert.NotNull(instance);
                Assert.NotNull(instance.Dependency1);
                Assert.NotNull(instance.Dependency2);
                Assert.True(instance.UsedLargerConstructor);
            }

            [Fact]
            public void ThrowsWhenTypeHasNoPublicConstructors()
            {
                var di = new MagicDI();

                var exception = Assert.Throws<InvalidOperationException>(() => di.Resolve<ClassWithNoPublicConstructor>());
                Assert.Contains("no public constructors", exception.Message);
            }

            [Fact]
            public void PropagatesExceptionsThrownByConstructors()
            {
                var di = new MagicDI();

                Assert.Throws<System.Reflection.TargetInvocationException>(() => di.Resolve<ClassWithThrowingConstructor>());
            }

            [Fact]
            public void ResolvesDeepDependencyChains()
            {
                var di = new MagicDI();

                var instance = di.Resolve<DeepLevel5>();

                Assert.NotNull(instance);
                Assert.NotNull(instance.Level4);
                Assert.NotNull(instance.Level4.Level3);
                Assert.NotNull(instance.Level4.Level3.Level2);
                Assert.NotNull(instance.Level4.Level3.Level2.Level1);
            }

            [Fact]
            public void SelectsConstructorDeterministicallyWhenParameterCountsMatch()
            {
                var di = new MagicDI();

                var instance1 = di.Resolve<ClassWithSameParameterCountConstructors>();
                var instance2 = di.Resolve<ClassWithSameParameterCountConstructors>();

                Assert.NotNull(instance1);
                Assert.NotNull(instance2);
            }

            [Fact]
            public void ReturnsCorrectTypeFromGenericResolve()
            {
                var di = new MagicDI();

                var instance = di.Resolve<SimpleClass>();

                Assert.NotNull(instance);
                Assert.IsType<SimpleClass>(instance);
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
