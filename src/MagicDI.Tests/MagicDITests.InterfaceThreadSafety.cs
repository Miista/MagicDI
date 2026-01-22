using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

// ReSharper disable ClassNeverInstantiated.Global

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class InterfaceThreadSafety
        {
            public class InterfaceSingletonGuarantee
            {
                [Fact]
                public async Task Concurrent_interface_resolves_return_same_singleton_instance()
                {
                    // Arrange
                    const int threadCount = 10;

                    var di = new MagicDI();
                    var instances = new ConcurrentBag<IThreadSafeService>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        var instance = di.Resolve<IThreadSafeService>();
                        instances.Add(instance);
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    var uniqueInstances = instances.Distinct().Count();
                    uniqueInstances.Should().Be(1,
                        because: "all threads should receive the same singleton instance when resolving via interface");
                }

                [Fact]
                public async Task Interface_singleton_constructor_is_called_exactly_once()
                {
                    // Arrange
                    const int threadCount = 20;

                    InterfaceInstanceCountingService.ResetCounter();
                    var di = new MagicDI();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        di.Resolve<IInstanceCountingService>();
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    InterfaceInstanceCountingService.InstanceCount.Should().Be(1,
                        because: "singleton constructor must only be invoked once");
                }
            }

            public class ContextStackIsolation
            {
                [Fact]
                public async Task Context_stacks_are_isolated_between_threads()
                {
                    // Arrange
                    const int threadCount = 10;

                    var di = new MagicDI();
                    var exceptions = new ConcurrentBag<Exception>();
                    var barrier = new Barrier(threadCount);
                    var results = new ConcurrentBag<INestedInterfaceService>();

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        try
                        {
                            barrier.SignalAndWait();
                            var instance = di.Resolve<INestedInterfaceService>();
                            results.Add(instance);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    exceptions.Should().BeEmpty(
                        because: "context stack isolation should prevent cross-thread contamination");
                    results.Should().HaveCount(threadCount);
                }

                [Fact]
                public async Task Nested_interface_resolution_maintains_correct_context_per_thread()
                {
                    // Arrange
                    const int threadCount = 50;

                    var di = new MagicDI();
                    var exceptions = new ConcurrentBag<Exception>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        try
                        {
                            barrier.SignalAndWait();
                            var instance = di.Resolve<IDeepInterfaceLevel3>();
                            instance.Should().NotBeNull();
                            instance.Level2.Should().NotBeNull();
                            instance.Level2.Level1.Should().NotBeNull();
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    exceptions.Should().BeEmpty(
                        because: "deeply nested interface resolution should work correctly under concurrent load");
                }
            }

            public class NestedInterfaceConcurrency
            {
                [Fact]
                public async Task Nested_interface_singleton_dependencies_are_shared_across_threads()
                {
                    // Arrange
                    const int threadCount = 10;

                    var di = new MagicDI();
                    var sharedDependencies = new ConcurrentBag<ISharedInterfaceDependency>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        var parent = di.Resolve<IParentWithInterfaceDependency>();
                        sharedDependencies.Add(parent.Dependency);
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    var uniqueDependencies = sharedDependencies.Distinct().Count();
                    uniqueDependencies.Should().Be(1,
                        because: "all parents should share the same singleton interface dependency");
                }

                [Fact]
                public async Task Interface_resolution_chain_is_thread_safe()
                {
                    // Arrange
                    const int threadCount = 20;

                    var di = new MagicDI();
                    var results = new ConcurrentBag<IInterfaceChainEnd>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        var instance = di.Resolve<IInterfaceChainEnd>();
                        results.Add(instance);
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    var uniqueInstances = results.Distinct().Count();
                    uniqueInstances.Should().Be(1,
                        because: "interface chain resolution should maintain singleton semantics");
                }
            }

            public class MixedResolution
            {
                [Fact]
                public async Task Concurrent_mixed_interface_and_concrete_resolution_works()
                {
                    // Arrange
                    const int threadCount = 100;

                    var di = new MagicDI();
                    var exceptions = new ConcurrentBag<Exception>();

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
                    {
                        try
                        {
                            if (i % 2 == 0)
                                di.Resolve<IThreadSafeService>();
                            else
                                di.Resolve<ThreadSafeServiceImpl>();
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    exceptions.Should().BeEmpty(
                        because: "mixed interface and concrete resolution should be thread-safe");
                }

                [Fact]
                public async Task Interface_and_concrete_resolve_to_same_singleton()
                {
                    // Arrange
                    const int threadCount = 20;

                    var di = new MagicDI();
                    var interfaceInstances = new ConcurrentBag<IThreadSafeService>();
                    var concreteInstances = new ConcurrentBag<ThreadSafeServiceImpl>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        if (i % 2 == 0)
                            interfaceInstances.Add(di.Resolve<IThreadSafeService>());
                        else
                            concreteInstances.Add(di.Resolve<ThreadSafeServiceImpl>());
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    var allInstances = interfaceInstances.Cast<object>()
                        .Concat(concreteInstances.Cast<object>())
                        .Distinct()
                        .Count();

                    allInstances.Should().Be(1,
                        because: "interface and concrete resolution should return the same singleton");
                }
            }

            public class LifetimeDeterminationConcurrency
            {
                [Fact]
                public async Task Concurrent_lifetime_determination_for_interfaces_is_consistent()
                {
                    // Arrange
                    const int threadCount = 50;

                    var di = new MagicDI();
                    var results = new ConcurrentBag<bool>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        var instance1 = di.Resolve<ITransientInterfaceService>();
                        var instance2 = di.Resolve<ITransientInterfaceService>();
                        var isTransient = !ReferenceEquals(instance1, instance2);
                        results.Add(isTransient);
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    results.All(r => r).Should().BeTrue(
                        because: "lifetime determination should be consistent across threads");
                }
            }

            public class TransientInterfaceConcurrency
            {
                [Fact]
                public async Task Transient_interface_creates_new_instance_per_resolution()
                {
                    // Arrange
                    const int threadCount = 10;
                    const int resolutionsPerThread = 5;

                    var di = new MagicDI();
                    var instances = new ConcurrentBag<ITransientInterfaceService>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        for (int i = 0; i < resolutionsPerThread; i++)
                        {
                            instances.Add(di.Resolve<ITransientInterfaceService>());
                        }
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    var uniqueInstances = instances.Distinct().Count();
                    uniqueInstances.Should().Be(threadCount * resolutionsPerThread,
                        because: "transient interface resolution should create a new instance each time");
                }
            }

            public class AmbiguityDetectionConcurrency
            {
                [Fact]
                public async Task Multiple_implementations_throws_consistently_under_concurrent_load()
                {
                    // Arrange
                    const int threadCount = 20;

                    var di = new MagicDI();
                    var exceptions = new ConcurrentBag<Exception>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        try
                        {
                            di.Resolve<IAmbiguousInterface>();
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    exceptions.Should().HaveCount(threadCount,
                        because: "all threads should receive the ambiguity exception");
                    exceptions.All(e => e is InvalidOperationException).Should().BeTrue();
                    exceptions.All(e => e.Message.Contains("Multiple implementations")).Should().BeTrue();
                }
            }

            #region Test Interfaces and Implementations

            public interface IThreadSafeService
            {
                void DoWork();
            }

            public class ThreadSafeServiceImpl : IThreadSafeService
            {
                public void DoWork() { }
            }

            public interface IInstanceCountingService
            {
                int GetCount();
            }

            public class InterfaceInstanceCountingService : IInstanceCountingService
            {
                private static int _instanceCount;
                public static int InstanceCount => _instanceCount;

                public InterfaceInstanceCountingService()
                {
                    Interlocked.Increment(ref _instanceCount);
                }

                public static void ResetCounter() => _instanceCount = 0;
                public int GetCount() => InstanceCount;
            }

            public interface ISharedInterfaceDependency { }

            public class SharedInterfaceDependencyImpl : ISharedInterfaceDependency { }

            public interface IParentWithInterfaceDependency
            {
                ISharedInterfaceDependency Dependency { get; }
            }

            public class ParentWithInterfaceDependencyImpl : IParentWithInterfaceDependency
            {
                public ISharedInterfaceDependency Dependency { get; }

                public ParentWithInterfaceDependencyImpl(ISharedInterfaceDependency dep)
                {
                    Dependency = dep;
                }
            }

            public interface INestedInterfaceService
            {
                ISharedInterfaceDependency InnerService { get; }
            }

            public class NestedInterfaceServiceImpl : INestedInterfaceService
            {
                public ISharedInterfaceDependency InnerService { get; }

                public NestedInterfaceServiceImpl(ISharedInterfaceDependency inner)
                {
                    InnerService = inner;
                }
            }

            public interface IDeepInterfaceLevel1 { }

            public class DeepInterfaceLevel1Impl : IDeepInterfaceLevel1 { }

            public interface IDeepInterfaceLevel2
            {
                IDeepInterfaceLevel1 Level1 { get; }
            }

            public class DeepInterfaceLevel2Impl : IDeepInterfaceLevel2
            {
                public IDeepInterfaceLevel1 Level1 { get; }

                public DeepInterfaceLevel2Impl(IDeepInterfaceLevel1 l1)
                {
                    Level1 = l1;
                }
            }

            public interface IDeepInterfaceLevel3
            {
                IDeepInterfaceLevel2 Level2 { get; }
            }

            public class DeepInterfaceLevel3Impl : IDeepInterfaceLevel3
            {
                public IDeepInterfaceLevel2 Level2 { get; }

                public DeepInterfaceLevel3Impl(IDeepInterfaceLevel2 l2)
                {
                    Level2 = l2;
                }
            }

            public interface IInterfaceChainStart { }

            public class InterfaceChainStartImpl : IInterfaceChainStart { }

            public interface IInterfaceChainMiddle
            {
                IInterfaceChainStart Start { get; }
            }

            public class InterfaceChainMiddleImpl : IInterfaceChainMiddle
            {
                public IInterfaceChainStart Start { get; }

                public InterfaceChainMiddleImpl(IInterfaceChainStart s)
                {
                    Start = s;
                }
            }

            public interface IInterfaceChainEnd
            {
                IInterfaceChainMiddle Middle { get; }
            }

            public class InterfaceChainEndImpl : IInterfaceChainEnd
            {
                public IInterfaceChainMiddle Middle { get; }

                public InterfaceChainEndImpl(IInterfaceChainMiddle m)
                {
                    Middle = m;
                }
            }

            public interface ITransientInterfaceService
            {
                void DoWork();
            }

            public class TransientInterfaceServiceImpl : ITransientInterfaceService, IDisposable
            {
                public void DoWork() { }
                public void Dispose() { }
            }

            public interface IAmbiguousInterface
            {
                void DoSomething();
            }

            public class AmbiguousImplA : IAmbiguousInterface
            {
                public void DoSomething() { }
            }

            public class AmbiguousImplB : IAmbiguousInterface
            {
                public void DoSomething() { }
            }

            #endregion
        }
    }
}
