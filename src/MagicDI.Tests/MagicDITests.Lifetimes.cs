using System;
using FluentAssertions;
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
                public void Type_with_no_dependencies_is_singleton()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<LeafClass>();
                    var instance2 = di.Resolve<LeafClass>();

                    // Assert
                    instance1.Should().BeSameAs(instance2, because: "types with no dependencies default to singleton");
                }
            }

            public class SingletonCascade
            {
                [Fact]
                public void Type_is_singleton_if_all_dependencies_are_singleton()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<ClassWithSingletonDeps>();
                    var instance2 = di.Resolve<ClassWithSingletonDeps>();

                    // Assert
                    instance1.Should().BeSameAs(instance2, because: "a class with all singleton dependencies should also be singleton");
                }
            }

            public class TransientCascade
            {
                [Fact]
                public void Type_with_transient_dependency_becomes_transient()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<ClassDependingOnDisposable>();
                    var instance2 = di.Resolve<ClassDependingOnDisposable>();

                    // Assert
                    instance1.Should().NotBeSameAs(instance2, because: "transient lifetime cascades up to dependent classes");
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
                    instance1.Disposable.Should().NotBeSameAs(instance2.Disposable, because: "transient dependencies must be recreated for each resolution");
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
                    instance1.Should().NotBeSameAs(instance2, because: "IDisposable types are inferred as transient to allow proper disposal");
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
                    IsolatedDisposable.InstanceCount.Should().Be(3, because: "each resolution of a transient type must invoke the constructor");
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
                    instance1.Should().BeSameAs(instance2, because: "[Lifetime(Singleton)] attribute overrides IDisposable inference");
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
                    instance1.Should().NotBeSameAs(instance2, because: "[Lifetime(Transient)] attribute forces transient behavior");
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
                    instance1.Should().BeSameAs(instance2, because: "[Lifetime(Singleton)] attribute overrides transient cascade from dependencies");
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
                    instance1.Should().NotBeSameAs(instance2, because: "transient lifetime cascades through the entire dependency chain");
                    instance1.Level2.Should().NotBeSameAs(instance2.Level2, because: "intermediate dependencies also become transient");
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
                    instance1.Should().NotBeSameAs(instance2, because: "the transient dependency makes the whole class transient");
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
                    instance1.SingletonDep.Should().BeSameAs(instance2.SingletonDep, because: "singleton dependencies retain their singleton behavior even in transient parents");
                }
            }

            #region Test Helper Classes

            public class LeafClass { }

            public class SingletonDep1 { }
            public class SingletonDep2 { }

            public class ClassWithSingletonDeps(SingletonDep1 dep1, SingletonDep2 dep2)
            {
                public SingletonDep1 Dep1 { get; } = dep1;
                public SingletonDep2 Dep2 { get; } = dep2;
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
