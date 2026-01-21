using System;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class Lifetimes
        {
            public class LeafNodes
            {
                [Fact]
                public void Leaf_classes_with_no_dependencies_are_singleton()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<LeafClass>();
                    var instance2 = di.Resolve<LeafClass>();

                    // Assert
                    Assert.Same(instance1, instance2);
                }
            }

            public class SingletonCascade
            {
                [Fact]
                public void Classes_with_all_singleton_dependencies_are_singleton()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<ClassWithSingletonDeps>();
                    var instance2 = di.Resolve<ClassWithSingletonDeps>();

                    // Assert
                    Assert.Same(instance1, instance2);
                }
            }

            public class TransientCascade
            {
                [Fact]
                public void Classes_depending_on_transient_become_transient()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<ClassDependingOnDisposable>();
                    var instance2 = di.Resolve<ClassDependingOnDisposable>();

                    // Assert
                    Assert.NotSame(instance1, instance2);
                }

                [Fact]
                public void Transient_dependencies_are_new_each_time()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<ClassDependingOnDisposable>();
                    var instance2 = di.Resolve<ClassDependingOnDisposable>();

                    // Assert
                    Assert.NotSame(instance1.Disposable, instance2.Disposable);
                }
            }

            public class DisposableInference
            {
                [Fact]
                public void Disposable_classes_are_transient()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<DisposableClass>();
                    var instance2 = di.Resolve<DisposableClass>();

                    // Assert
                    Assert.NotSame(instance1, instance2);
                }

                [Fact]
                public void Disposable_classes_create_new_instance_each_time()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    di.Resolve<IsolatedDisposable>();
                    di.Resolve<IsolatedDisposable>();
                    di.Resolve<IsolatedDisposable>();

                    // Assert
                    Assert.Equal(3, IsolatedDisposable.InstanceCount);
                }

                public class IsolatedDisposable : IDisposable
                {
                    public static int InstanceCount;
                    public IsolatedDisposable() => InstanceCount++;
                    public void Dispose() { }
                }
            }

            public class AttributeOverrides
            {
                [Fact]
                public void Singleton_attribute_overrides_disposable_inference()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<DisposableSingletonClass>();
                    var instance2 = di.Resolve<DisposableSingletonClass>();

                    // Assert
                    Assert.Same(instance1, instance2);
                }

                [Fact]
                public void Transient_attribute_is_respected()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<ExplicitTransientClass>();
                    var instance2 = di.Resolve<ExplicitTransientClass>();

                    // Assert
                    Assert.NotSame(instance1, instance2);
                }

                [Fact]
                public void Singleton_attribute_overrides_transient_cascade()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<SingletonWithTransientDep>();
                    var instance2 = di.Resolve<SingletonWithTransientDep>();

                    // Assert
                    Assert.Same(instance1, instance2);
                }
            }

            public class ComplexScenarios
            {
                [Fact]
                public void Transient_cascades_up_entire_dependency_chain()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - Chain: Level3 -> Level2 -> DisposableClass (Transient)
                    var instance1 = di.Resolve<TransientChainLevel3>();
                    var instance2 = di.Resolve<TransientChainLevel3>();

                    // Assert
                    Assert.NotSame(instance1, instance2);
                    Assert.NotSame(instance1.Level2, instance2.Level2);
                }

                [Fact]
                public void Least_cacheable_lifetime_wins_with_mixed_dependencies()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - Has one Singleton dep and one Transient dep
                    var instance1 = di.Resolve<MixedDependenciesClass>();
                    var instance2 = di.Resolve<MixedDependenciesClass>();

                    // Assert
                    Assert.NotSame(instance1, instance2);
                }

                [Fact]
                public void Singleton_dependencies_are_shared_across_transient_parents()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<MixedDependenciesClass>();
                    var instance2 = di.Resolve<MixedDependenciesClass>();

                    // Assert
                    Assert.Same(instance1.SingletonDep, instance2.SingletonDep);
                }
            }

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
