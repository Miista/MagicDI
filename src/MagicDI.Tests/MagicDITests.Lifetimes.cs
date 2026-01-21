using System;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class Lifetimes
        {
            #region Leaf Node Tests (No Dependencies)

            [Fact]
            public void Resolve_LeafClassNoDependencies_IsSingleton()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<LeafClass>();
                var instance2 = di.Resolve<LeafClass>();

                // Assert - Leaf classes with no deps should be Singleton
                Assert.Same(instance1, instance2);
            }

            #endregion

            #region Singleton Cascade Tests

            [Fact]
            public void Resolve_ClassWithAllSingletonDependencies_IsSingleton()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<ClassWithSingletonDeps>();
                var instance2 = di.Resolve<ClassWithSingletonDeps>();

                // Assert - Class with all Singleton deps should also be Singleton
                Assert.Same(instance1, instance2);
            }

            #endregion

            #region Transient Cascade Tests

            [Fact]
            public void Resolve_ClassDependingOnTransient_IsTransient()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<ClassDependingOnDisposable>();
                var instance2 = di.Resolve<ClassDependingOnDisposable>();

                // Assert - Class depending on Transient should cascade to Transient
                Assert.NotSame(instance1, instance2);
            }

            [Fact]
            public void Resolve_ClassDependingOnTransient_DependencyIsNewEachTime()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<ClassDependingOnDisposable>();
                var instance2 = di.Resolve<ClassDependingOnDisposable>();

                // Assert - Each resolution gets a new disposable dependency
                Assert.NotSame(instance1.Disposable, instance2.Disposable);
            }

            #endregion

            #region IDisposable Tests

            [Fact]
            public void Resolve_DisposableClass_IsTransient()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<DisposableClass>();
                var instance2 = di.Resolve<DisposableClass>();

                // Assert - IDisposable types should be Transient
                Assert.NotSame(instance1, instance2);
            }

            [Fact]
            public void Resolve_DisposableClass_CreatesNewInstanceEachTime()
            {
                // Arrange
                var di = new MagicDI();
                DisposableClass.ResetCounter();

                // Act
                di.Resolve<DisposableClass>();
                di.Resolve<DisposableClass>();
                di.Resolve<DisposableClass>();

                // Assert - Each resolution should create a new instance
                Assert.Equal(3, DisposableClass.InstanceCount);
            }

            #endregion

            #region Attribute Override Tests

            [Fact]
            public void Resolve_DisposableWithSingletonAttribute_IsSingleton()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<DisposableSingletonClass>();
                var instance2 = di.Resolve<DisposableSingletonClass>();

                // Assert - Attribute overrides IDisposable inference
                Assert.Same(instance1, instance2);
            }

            [Fact]
            public void Resolve_ClassWithTransientAttribute_IsTransient()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<ExplicitTransientClass>();
                var instance2 = di.Resolve<ExplicitTransientClass>();

                // Assert - Explicit Transient attribute is respected
                Assert.NotSame(instance1, instance2);
            }

            [Fact]
            public void Resolve_ClassWithSingletonAttributeAndTransientDep_IsSingleton()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<SingletonWithTransientDep>();
                var instance2 = di.Resolve<SingletonWithTransientDep>();

                // Assert - Attribute overrides cascade behavior
                Assert.Same(instance1, instance2);
            }

            #endregion

            #region Complex Scenarios

            [Fact]
            public void Resolve_DeepChainWithOneTransient_AllAncestorsAreTransient()
            {
                // Arrange
                var di = new MagicDI();

                // Act - Chain: Level3 -> Level2 -> DisposableClass (Transient)
                var instance1 = di.Resolve<TransientChainLevel3>();
                var instance2 = di.Resolve<TransientChainLevel3>();

                // Assert - The Transient should cascade up the entire chain
                Assert.NotSame(instance1, instance2);
                Assert.NotSame(instance1.Level2, instance2.Level2);
            }

            [Fact]
            public void Resolve_MixedDependencies_LeastCacheableWins()
            {
                // Arrange
                var di = new MagicDI();

                // Act - Has one Singleton dep and one Transient dep
                var instance1 = di.Resolve<MixedDependenciesClass>();
                var instance2 = di.Resolve<MixedDependenciesClass>();

                // Assert - Transient wins, so parent is Transient
                Assert.NotSame(instance1, instance2);
            }

            [Fact]
            public void Resolve_SingletonDependency_IsCachedAcrossTransientParents()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                var instance1 = di.Resolve<MixedDependenciesClass>();
                var instance2 = di.Resolve<MixedDependenciesClass>();

                // Assert - The Singleton dependency should be the same across both
                Assert.Same(instance1.SingletonDep, instance2.SingletonDep);
            }

            #endregion

            #region Test Helper Classes

            public class LeafClass { }

            public class SingletonDep1 { }
            public class SingletonDep2 { }

            public class ClassWithSingletonDeps
            {
                public SingletonDep1 Dep1 { get; }
                public SingletonDep2 Dep2 { get; }

                public ClassWithSingletonDeps(SingletonDep1 dep1, SingletonDep2 dep2)
                {
                    Dep1 = dep1;
                    Dep2 = dep2;
                }
            }

            public class DisposableClass : IDisposable
            {
                private static int _instanceCount;
                public static int InstanceCount => _instanceCount;

                public DisposableClass()
                {
                    System.Threading.Interlocked.Increment(ref _instanceCount);
                }

                public static void ResetCounter() => _instanceCount = 0;

                public void Dispose() { }
            }

            public class ClassDependingOnDisposable
            {
                public DisposableClass Disposable { get; }

                public ClassDependingOnDisposable(DisposableClass disposable)
                {
                    Disposable = disposable;
                }
            }

            [Lifetime(Lifetime.Singleton)]
            public class DisposableSingletonClass : IDisposable
            {
                public void Dispose() { }
            }

            [Lifetime(Lifetime.Transient)]
            public class ExplicitTransientClass { }

            [Lifetime(Lifetime.Singleton)]
            public class SingletonWithTransientDep
            {
                public DisposableClass Disposable { get; }

                public SingletonWithTransientDep(DisposableClass disposable)
                {
                    Disposable = disposable;
                }
            }

            // Chain for testing cascade: Level3 -> Level2 -> DisposableClass
            public class TransientChainLevel2
            {
                public DisposableClass Disposable { get; }

                public TransientChainLevel2(DisposableClass disposable)
                {
                    Disposable = disposable;
                }
            }

            public class TransientChainLevel3
            {
                public TransientChainLevel2 Level2 { get; }

                public TransientChainLevel3(TransientChainLevel2 level2)
                {
                    Level2 = level2;
                }
            }

            public class MixedDependenciesClass
            {
                public LeafClass SingletonDep { get; }
                public DisposableClass TransientDep { get; }

                public MixedDependenciesClass(LeafClass singletonDep, DisposableClass transientDep)
                {
                    SingletonDep = singletonDep;
                    TransientDep = transientDep;
                }
            }

            #endregion
        }
    }
}
