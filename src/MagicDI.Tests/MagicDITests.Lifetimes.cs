using System;
using FluentAssertions;
using Xunit;

// ReSharper disable ClassNeverInstantiated.Global

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class Lifetimes
        {
            public class BasicSingletonBehavior
            {
                [Fact]
                public void Returns_same_instance_for_singleton_lifetime()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<LeafClass>();
                    var instance2 = di.Resolve<LeafClass>();

                    // Assert
                    instance1.Should().BeSameAs(instance2, because: "singleton lifetime means the same instance is returned for all resolutions");
                }

                [Fact]
                public void Shares_singleton_instances_across_dependency_graphs()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<SingletonParent>();
                    var instance2 = di.Resolve<SingletonGrandparent>();

                    // Assert
                    instance1.Leaf.Should().BeSameAs(instance2.Parent.Leaf, because: "singleton instances should be shared across different dependency graphs");
                }

                public class SingletonParent(LeafClass leaf)
                {
                    public LeafClass Leaf { get; } = leaf;
                }

                public class SingletonGrandparent(SingletonParent parent)
                {
                    public SingletonParent Parent { get; } = parent;
                }
            }

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

            public class InferencePriority
            {
                [Fact]
                public void Attribute_takes_priority_over_disposable_inference()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - DisposableWithSingletonAttr is IDisposable but has [Lifetime(Singleton)]
                    var instance1 = di.Resolve<DisposableWithSingletonAttr>();
                    var instance2 = di.Resolve<DisposableWithSingletonAttr>();

                    // Assert
                    instance1.Should().BeSameAs(instance2, because: "attribute (priority 1) beats IDisposable inference (priority 2)");
                }

                [Fact]
                public void Transient_attribute_overrides_default_singleton_for_leaf_node()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance1 = di.Resolve<ExplicitTransientClass>();
                    var instance2 = di.Resolve<ExplicitTransientClass>();

                    // Assert
                    instance1.Should().NotBeSameAs(instance2, because: "[Lifetime(Transient)] attribute overrides the default singleton inference for leaf nodes");
                }

                [Fact]
                public void Disposable_takes_priority_over_dependency_cascade()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - DisposableWithSingletonDeps implements IDisposable but has all singleton deps
                    var instance1 = di.Resolve<DisposableWithSingletonDeps>();
                    var instance2 = di.Resolve<DisposableWithSingletonDeps>();

                    // Assert
                    instance1.Should().NotBeSameAs(instance2, because: "IDisposable (priority 2) beats singleton cascade (priority 3)");
                }

                [Fact]
                public void Dependency_cascade_takes_priority_over_default_singleton()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - ClassDependingOnDisposable has no attribute, not IDisposable, but has transient dep
                    var instance1 = di.Resolve<ClassDependingOnDisposable>();
                    var instance2 = di.Resolve<ClassDependingOnDisposable>();

                    // Assert
                    instance1.Should().NotBeSameAs(instance2, because: "dependency cascade (priority 3) beats default singleton (priority 4)");
                }

                [Fact]
                public void Default_is_singleton_when_no_other_rules_apply()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - LeafClass has no attribute, not IDisposable, no deps
                    var instance1 = di.Resolve<LeafClass>();
                    var instance2 = di.Resolve<LeafClass>();

                    // Assert
                    instance1.Should().BeSameAs(instance2, because: "default (priority 4) is singleton when no other rules apply");
                }

                // Test helper class: IDisposable with all singleton dependencies
                public class DisposableWithSingletonDeps(LeafClass dep) : IDisposable
                {
                    public LeafClass Dep { get; } = dep;

                    public void Dispose() { }
                }

                // Test helper class: IDisposable with explicit Singleton attribute
                [Lifetime(Lifetime.Singleton)]
                public class DisposableWithSingletonAttr : IDisposable
                {
                    public void Dispose() { }
                }
            }

            public class CaptiveDependency
            {
                [Fact]
                public void Throws_when_explicit_singleton_depends_on_transient()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<SingletonWithTransientDep>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage("*Captive dependency detected*", because: "explicit singleton with transient dependency is a configuration error");
                }

                [Fact]
                public void Exception_message_includes_type_names()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<SingletonWithTransientDep>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage("*SingletonWithTransientDep*")
                        .WithMessage("*DisposableClass*", because: "exception should identify both the singleton and its transient dependency");
                }

                [Fact]
                public void Does_not_throw_when_singleton_depends_on_singleton()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<ClassWithSingletonDeps>();

                    // Assert
                    act.Should().NotThrow(because: "singleton depending on singleton is valid");
                }

                [Fact]
                public void Does_not_throw_for_inferred_transient_cascade()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - No explicit attribute, so cascade is fine
                    Action act = () => di.Resolve<ClassDependingOnDisposable>();

                    // Assert
                    act.Should().NotThrow(because: "inferred transient cascade without explicit attribute is valid");
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

            public class SingletonAcrossCallContexts
            {
                [Fact]
                public async System.Threading.Tasks.Task Singleton_shared_across_async_contexts()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var task1 = System.Threading.Tasks.Task.Run(() => di.Resolve<LeafClass>());
                    var task2 = System.Threading.Tasks.Task.Run(() => di.Resolve<ClassWithSingletonDeps>());
                    var task3 = System.Threading.Tasks.Task.Run(() => di.Resolve<LeafClass>());

                    await System.Threading.Tasks.Task.WhenAll(task1, task2, task3);
                    var result1 = await task1;
                    var result2 = await task2;
                    var result3 = await task3;

                    // Assert
                    result1.Should().NotBeNull();
                    result2.Should().NotBeNull();
                    result3.Should().NotBeNull();
                    result1.Should().BeSameAs(result3,
                        because: "singleton instances should be shared across async contexts");
                }

                [Fact]
                public void Nested_resolve_calls_maintain_singleton_sharing()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithSingletonDeps>();

                    // Assert
                    instance.Should().NotBeNull();
                    instance.Dep1.Should().NotBeNull(
                        because: "nested dependencies should be resolved during the resolution chain");
                }

                [Fact]
                public void Singleton_shared_across_nested_and_direct_resolutions()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var parent = di.Resolve<ClassWithSingletonDeps>();
                    var directDep = di.Resolve<SingletonDep1>();

                    // Assert
                    parent.Dep1.Should().BeSameAs(directDep,
                        because: "singleton instances should be shared across nested and direct resolutions");
                }
            }

            public class ContainerIsolation
            {
                [Fact]
                public void Singletons_are_isolated_per_container_instance()
                {
                    // Arrange
                    var container1 = new MagicDI();
                    var container2 = new MagicDI();

                    // Act
                    var instance1 = container1.Resolve<IsolatedSingleton>();
                    var instance2 = container2.Resolve<IsolatedSingleton>();

                    // Assert
                    instance1.Should().NotBeSameAs(instance2,
                        because: "each container should have its own singleton cache");
                }

                [Fact]
                public void Same_container_returns_same_singleton()
                {
                    // Arrange
                    var container = new MagicDI();

                    // Act
                    var instance1 = container.Resolve<IsolatedSingleton>();
                    var instance2 = container.Resolve<IsolatedSingleton>();

                    // Assert
                    instance1.Should().BeSameAs(instance2,
                        because: "the same container should return the same singleton instance");
                }

                [Fact]
                public void Nested_dependencies_are_isolated_per_container()
                {
                    // Arrange
                    var container1 = new MagicDI();
                    var container2 = new MagicDI();

                    // Act
                    var parent1 = container1.Resolve<IsolatedParent>();
                    var parent2 = container2.Resolve<IsolatedParent>();

                    // Assert
                    parent1.Child.Should().NotBeSameAs(parent2.Child,
                        because: "nested singleton dependencies should also be isolated per container");
                }

                [Fact]
                public void Lifetime_determination_is_isolated_per_container()
                {
                    // Arrange
                    var container1 = new MagicDI();
                    var container2 = new MagicDI();

                    // Act - resolve the same type from both containers
                    var instance1a = container1.Resolve<IsolatedSingleton>();
                    var instance1b = container1.Resolve<IsolatedSingleton>();
                    var instance2a = container2.Resolve<IsolatedSingleton>();
                    var instance2b = container2.Resolve<IsolatedSingleton>();

                    // Assert
                    instance1a.Should().BeSameAs(instance1b,
                        because: "container1 should cache its own singleton");
                    instance2a.Should().BeSameAs(instance2b,
                        because: "container2 should cache its own singleton");
                    instance1a.Should().NotBeSameAs(instance2a,
                        because: "containers should not share lifetime state");
                }

                [Fact]
                public void Transient_types_create_new_instances_regardless_of_container()
                {
                    // Arrange
                    var container1 = new MagicDI();
                    var container2 = new MagicDI();

                    // Act
                    var instance1a = container1.Resolve<IsolatedTransient>();
                    var instance1b = container1.Resolve<IsolatedTransient>();
                    var instance2a = container2.Resolve<IsolatedTransient>();
                    var instance2b = container2.Resolve<IsolatedTransient>();

                    // Assert - all four should be different
                    instance1a.Should().NotBeSameAs(instance1b,
                        because: "transient types always create new instances");
                    instance2a.Should().NotBeSameAs(instance2b,
                        because: "transient types always create new instances");
                    instance1a.Should().NotBeSameAs(instance2a,
                        because: "transient types from different containers are also distinct");
                }

                public class IsolatedSingleton;

                public class IsolatedParent(IsolatedSingleton child)
                {
                    public IsolatedSingleton Child { get; } = child;
                }

                public class IsolatedTransient : IDisposable
                {
                    public void Dispose() { }
                }
            }

            public class AttributeInheritance
            {
                [Fact]
                public void Derived_class_inherits_lifetime_from_base()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - DerivedFromTransient has no attribute but base is Transient
                    var instance1 = di.Resolve<DerivedFromTransient>();
                    var instance2 = di.Resolve<DerivedFromTransient>();

                    // Assert
                    instance1.Should().NotBeSameAs(instance2,
                        because: "derived class should inherit Transient lifetime from base");
                }

                [Fact]
                public void Derived_class_attribute_overrides_base()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - DerivedWithOverride has Singleton, base has Transient
                    var instance1 = di.Resolve<DerivedWithOverride>();
                    var instance2 = di.Resolve<DerivedWithOverride>();

                    // Assert
                    instance1.Should().BeSameAs(instance2,
                        because: "derived class attribute should override base class attribute");
                }

                [Fact]
                public void Abstract_class_lifetime_inherited_by_concrete()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - ConcreteFromAbstract inherits from abstract with Transient
                    var instance1 = di.Resolve<ConcreteFromAbstract>();
                    var instance2 = di.Resolve<ConcreteFromAbstract>();

                    // Assert
                    instance1.Should().NotBeSameAs(instance2,
                        because: "concrete class should inherit Transient from abstract base");
                }

                [Fact]
                public void Multi_level_inheritance_uses_closest_attribute()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - Level3 has no attr, Level2 has Singleton, Level1 has Transient
                    var instance1 = di.Resolve<InheritanceLevel3>();
                    var instance2 = di.Resolve<InheritanceLevel3>();

                    // Assert - should use Level2's Singleton (closest)
                    instance1.Should().BeSameAs(instance2,
                        because: "closest attribute in inheritance chain should win");
                }

                [Lifetime(Lifetime.Transient)]
                public class BaseWithTransient;

                public class DerivedFromTransient : BaseWithTransient;

                [Lifetime(Lifetime.Singleton)]
                public class DerivedWithOverride : BaseWithTransient;

                [Lifetime(Lifetime.Transient)]
                public abstract class AbstractWithTransient;

                public class ConcreteFromAbstract : AbstractWithTransient;

                // Multi-level inheritance chain
                [Lifetime(Lifetime.Transient)]
                public class InheritanceLevel1;

                [Lifetime(Lifetime.Singleton)]
                public class InheritanceLevel2 : InheritanceLevel1;

                public class InheritanceLevel3 : InheritanceLevel2;
            }

            #region Test Helper Classes

            public class LeafClass;

            public class SingletonDep1;
            public class SingletonDep2;

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

            public class ClassDependingOnDisposable(DisposableClass disposable)
            {
                public DisposableClass Disposable { get; } = disposable;
            }

            [Lifetime(Lifetime.Transient)]
            public class ExplicitTransientClass;

            [Lifetime(Lifetime.Singleton)]
            public class SingletonWithTransientDep(DisposableClass disposable)
            {
                public DisposableClass Disposable { get; } = disposable;
            }

            // Chain for testing cascade: Level3 -> Level2 -> DisposableClass
            public class TransientChainLevel2(DisposableClass disposable)
            {
                public DisposableClass Disposable { get; } = disposable;
            }

            public class TransientChainLevel3(TransientChainLevel2 level2)
            {
                public TransientChainLevel2 Level2 { get; } = level2;
            }

            public class MixedDependenciesClass(LeafClass singletonDep, DisposableClass transientDep)
            {
                public LeafClass SingletonDep { get; } = singletonDep;
                public DisposableClass TransientDep { get; } = transientDep;
            }

            #endregion
        }
    }
}
