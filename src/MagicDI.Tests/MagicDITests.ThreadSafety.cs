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
        public class ThreadSafety
        {
            public class SingletonGuarantee
            {
                [Fact]
                public async Task Concurrent_resolves_return_same_instance()
                {
                    // Arrange
                    const int threadCount = 10;
                    
                    var di = new MagicDI();
                    var instances = new ConcurrentBag<SlowConstructorClass>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        var instance = di.Resolve<SlowConstructorClass>();
                        instances.Add(instance);
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    var uniqueInstances = instances.Distinct().Count();
                    uniqueInstances.Should().Be(1, because: "all threads should receive the same singleton instance");
                }

                [Fact]
                public async Task Singleton_constructor_is_called_exactly_once()
                {
                    // Arrange
                    const int threadCount = 20;
                    
                    InstanceCountingClass.ResetCounter();
                    var di = new MagicDI();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        di.Resolve<InstanceCountingClass>();
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    InstanceCountingClass.InstanceCount.Should().Be(1, because: "singleton constructor must only be invoked once regardless of concurrent access");
                }
            }

            public class ConcurrentAccess
            {
                [Fact]
                public async Task Resolving_different_types_concurrently_does_not_throw()
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
                            switch (i % 5)
                            {
                                case 0: di.Resolve<ThreadTestClass1>(); break;
                                case 1: di.Resolve<ThreadTestClass2>(); break;
                                case 2: di.Resolve<ThreadTestClass3>(); break;
                                case 3: di.Resolve<ThreadTestClass4>(); break;
                                case 4: di.Resolve<ThreadTestClass5>(); break;
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    exceptions.Should().BeEmpty(because: "concurrent dictionary access should be thread-safe");
                }

                [Fact]
                public async Task Dependencies_remain_singleton_under_concurrent_load()
                {
                    // Arrange
                    const int threadCount = 10;
                    
                    var di = new MagicDI();
                    var sharedDependencies = new ConcurrentBag<SharedDependency>();
                    var barrier = new Barrier(threadCount);

                    // Act
                    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        var parent = di.Resolve<ParentWithSharedDependency>();
                        sharedDependencies.Add(parent.Dependency);
                    })).ToArray();

                    await Task.WhenAll(tasks);

                    // Assert
                    var uniqueDependencies = sharedDependencies.Distinct().Count();
                    uniqueDependencies.Should().Be(1, because: "all parents should share the same singleton dependency");
                }
            }

            #region Test Helper Classes

            public class SlowConstructorClass
            {
                public SlowConstructorClass()
                {
                    Thread.Sleep(50);
                }
            }

            public class InstanceCountingClass
            {
                private static int _instanceCount;
                public static int InstanceCount => _instanceCount;

                public InstanceCountingClass()
                {
                    Interlocked.Increment(ref _instanceCount);
                }

                public static void ResetCounter() => _instanceCount = 0;
            }

            public class ThreadTestClass1;
            public class ThreadTestClass2;
            public class ThreadTestClass3;
            public class ThreadTestClass4;
            public class ThreadTestClass5;

            public class SharedDependency;
            public class ParentWithSharedDependency(SharedDependency dependency)
            {
                public SharedDependency Dependency { get; } = dependency;
            }

            #endregion
        }
    }
}
