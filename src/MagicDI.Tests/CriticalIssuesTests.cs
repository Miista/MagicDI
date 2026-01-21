using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MagicDI.Tests
{
    /// <summary>
    /// Tests that reveal critical issues in the MagicDI implementation.
    /// These tests are expected to FAIL until the issues are fixed.
    /// </summary>
    public class CriticalIssuesTests
    {
        #region Issue 1: Thread Safety

        /// <summary>
        /// Reveals: Multiple threads resolving the same type simultaneously may create
        /// multiple instances, violating the singleton guarantee.
        /// </summary>
        [Fact]
        public async Task ThreadSafety_ConcurrentResolve_ShouldReturnSameInstance()
        {
            // Arrange
            var di = new MagicDI();
            var instances = new ConcurrentBag<SlowConstructorClass>();
            var barrier = new Barrier(10); // Synchronize 10 threads to start simultaneously

            // Act - 10 threads all try to resolve the same type at once
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait(); // Wait for all threads to be ready
                var instance = di.Resolve<SlowConstructorClass>();
                instances.Add(instance);
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert - All instances should be the same object (singleton)
            var uniqueInstances = instances.Distinct().Count();
            Assert.Equal(1, uniqueInstances);
        }

        /// <summary>
        /// Reveals: Race condition where two threads both check the cache, find nothing,
        /// and both create new instances.
        /// </summary>
        [Fact]
        public async Task ThreadSafety_RaceCondition_SingletonShouldBeCreatedOnlyOnce()
        {
            // Arrange
            InstanceCountingClass.ResetCounter();
            var di = new MagicDI();
            var barrier = new Barrier(20);

            // Act - 20 threads all try to resolve simultaneously
            var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait();
                di.Resolve<InstanceCountingClass>();
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert - Constructor should have been called exactly once
            Assert.Equal(1, InstanceCountingClass.InstanceCount);
        }

        /// <summary>
        /// Reveals: Dictionary may throw or corrupt when modified concurrently.
        /// </summary>
        [Fact]
        public async Task ThreadSafety_ConcurrentDifferentTypes_ShouldNotThrow()
        {
            // Arrange
            var di = new MagicDI();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - Many threads resolve different types concurrently
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
            {
                try
                {
                    // Resolve different types to stress the dictionary
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

            // Assert - No exceptions should have occurred
            Assert.Empty(exceptions);
        }

        /// <summary>
        /// Reveals: Under heavy concurrent load with dependencies, the container
        /// may produce inconsistent results.
        /// </summary>
        [Fact]
        public async Task ThreadSafety_ConcurrentWithDependencies_DependenciesShouldBeSingleton()
        {
            // Arrange
            var di = new MagicDI();
            var sharedDependencies = new ConcurrentBag<SharedDependency>();
            var barrier = new Barrier(10);

            // Act
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait();
                var parent = di.Resolve<ParentWithSharedDependency>();
                sharedDependencies.Add(parent.Dependency);
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert - All parents should have received the same dependency instance
            var uniqueDependencies = sharedDependencies.Distinct().Count();
            Assert.Equal(1, uniqueDependencies);
        }

        #endregion

        #region Test Helper Classes

        // Thread Safety Test Classes
        public class SlowConstructorClass
        {
            public SlowConstructorClass()
            {
                // Simulate slow construction to increase chance of race condition
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

        public class ThreadTestClass1 { }
        public class ThreadTestClass2 { }
        public class ThreadTestClass3 { }
        public class ThreadTestClass4 { }
        public class ThreadTestClass5 { }

        public class SharedDependency { }
        public class ParentWithSharedDependency
        {
            public SharedDependency Dependency { get; }
            public ParentWithSharedDependency(SharedDependency dependency)
            {
                Dependency = dependency;
            }
        }

        #endregion
    }
}
