using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        #region Issue 2: Lifetime Implementation (now working with magic inference)

        /// <summary>
        /// Transient lifetime via [Lifetime] attribute creates new instance each time.
        /// </summary>
        [Fact]
        public void Lifetime_TransientAttribute_ShouldCreateNewInstanceEachTime()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance1 = di.Resolve<TransientClass>();
            var instance2 = di.Resolve<TransientClass>();

            // Assert - Should be different instances (transient behavior)
            Assert.NotSame(instance1, instance2);
        }

        /// <summary>
        /// Lifetime can be specified via [Lifetime] attribute instead of registration API.
        /// </summary>
        [Fact]
        public void Lifetime_AttributeBasedConfiguration_WorksWithoutRegistration()
        {
            // Arrange
            var di = new MagicDI();

            // Act - Resolve a type with [Lifetime(Lifetime.Transient)] attribute
            var instance1 = di.Resolve<TransientClass>();
            var instance2 = di.Resolve<TransientClass>();

            // Assert - Transient via attribute works
            Assert.NotSame(instance1, instance2);
        }

        /// <summary>
        /// DetermineLifeTime respects [Lifetime] attribute configuration.
        /// </summary>
        [Fact]
        public void Lifetime_DetermineLifeTime_ShouldRespectTypeConfiguration()
        {
            // Arrange
            var di = new MagicDI();
            TransientClass.ResetCounter();

            // Act - Resolve 5 times
            for (int i = 0; i < 5; i++)
            {
                di.Resolve<TransientClass>();
            }

            // Assert - If properly transient, constructor should be called 5 times
            Assert.Equal(5, TransientClass.ConstructorCallCount);
        }

        /// <summary>
        /// IDisposable types are automatically inferred as Transient.
        /// </summary>
        [Fact]
        public void Lifetime_IDisposable_ShouldBeTransient()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance1 = di.Resolve<DisposableService>();
            var instance2 = di.Resolve<DisposableService>();

            // Assert - IDisposable types should be transient
            Assert.NotSame(instance1, instance2);
        }

        /// <summary>
        /// All Lifetime enum values are now usable via magic inference.
        /// </summary>
        [Fact]
        public void Lifetime_AllEnumValues_ShouldBeUsable()
        {
            // Arrange
            var lifetimeType = typeof(Lifetime);
            var values = Enum.GetValues(lifetimeType);

            // Assert - Multiple lifetime values exist and are now usable
            Assert.True(values.Length > 1,
                "Multiple lifetime values exist");

            // Verify Transient is now working
            var di = new MagicDI();
            var t1 = di.Resolve<TransientClass>();
            var t2 = di.Resolve<TransientClass>();
            Assert.NotSame(t1, t2);

            // Verify Singleton still works
            var s1 = di.Resolve<SingletonService>();
            var s2 = di.Resolve<SingletonService>();
            Assert.Same(s1, s2);
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

        // Lifetime Test Classes
        [Lifetime(Lifetime.Transient)]
        public class TransientClass
        {
            private static int _constructorCallCount;
            public static int ConstructorCallCount => _constructorCallCount;

            public TransientClass()
            {
                Interlocked.Increment(ref _constructorCallCount);
            }

            public static void ResetCounter() => _constructorCallCount = 0;
        }

        public class DisposableService : IDisposable
        {
            public void Dispose() { }
        }

        public class SingletonService { }

        #endregion
    }
}
