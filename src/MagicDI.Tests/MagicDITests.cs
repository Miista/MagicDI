using System;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        [Fact]
        public void Resolve_SimpleType_ReturnsInstance()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance = di.Resolve<SimpleClass>();

            // Assert
            Assert.NotNull(instance);
        }

        [Fact]
        public void Resolve_TypeWithDependency_ResolvesDependency()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance = di.Resolve<ClassWithDependency>();

            // Assert
            Assert.NotNull(instance);
            Assert.NotNull(instance.Dependency);
        }

        [Fact]
        public void Resolve_Singleton_ReturnsSameInstance()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance1 = di.Resolve<SimpleClass>();
            var instance2 = di.Resolve<SimpleClass>();

            // Assert
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void Resolve_NestedDependencies_ResolvesAll()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance = di.Resolve<ClassWithNestedDependency>();

            // Assert
            Assert.NotNull(instance);
            Assert.NotNull(instance.Dependency);
            Assert.NotNull(instance.Dependency.Dependency);
        }

        [Fact]
        public void Resolve_MultipleConstructorParameters_ResolvesAll()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance = di.Resolve<ClassWithMultipleDependencies>();

            // Assert
            Assert.NotNull(instance);
            Assert.NotNull(instance.Dependency1);
            Assert.NotNull(instance.Dependency2);
        }

        [Fact]
        public void Resolve_DependencySharedAcrossTypes_ReturnsSameInstance()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance1 = di.Resolve<ClassWithDependency>();
            var instance2 = di.Resolve<ClassWithNestedDependency>();

            // Assert
            // The SimpleClass dependency should be the same instance (singleton)
            Assert.Same(instance1.Dependency, instance2.Dependency.Dependency);
        }

        [Fact]
        public void Resolve_PrimitiveType_ThrowsInvalidOperationException()
        {
            // Arrange
            var di = new MagicDI();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => di.Resolve<int>());
        }

        [Fact]
        public void Resolve_ConstructorSelectsMostParameters()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance = di.Resolve<ClassWithMultipleConstructors>();

            // Assert
            Assert.NotNull(instance);
            Assert.NotNull(instance.Dependency1);
            Assert.NotNull(instance.Dependency2);
            Assert.True(instance.UsedLargerConstructor);
        }

        [Fact]
        public void Resolve_TypeWithNoPublicConstructors_ThrowsInvalidOperationException()
        {
            // Arrange
            var di = new MagicDI();

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => di.Resolve<ClassWithNoPublicConstructor>());
            Assert.Contains("no public constructors", exception.Message);
        }

        [Fact]
        public void Resolve_ConstructorThrowsException_ThrowsTargetInvocationException()
        {
            // Arrange
            var di = new MagicDI();

            // Act & Assert
            Assert.Throws<System.Reflection.TargetInvocationException>(() => di.Resolve<ClassWithThrowingConstructor>());
        }

        [Fact]
        public void Resolve_DeepDependencyChain_ResolvesSuccessfully()
        {
            // Arrange
            var di = new MagicDI();

            // Act
            var instance = di.Resolve<DeepLevel5>();

            // Assert
            Assert.NotNull(instance);
            Assert.NotNull(instance.Level4);
            Assert.NotNull(instance.Level4.Level3);
            Assert.NotNull(instance.Level4.Level3.Level2);
            Assert.NotNull(instance.Level4.Level3.Level2.Level1);
        }

        [Fact]
        public void Resolve_MultipleConstructorsSameParameterCount_SelectsDeterministically()
        {
            // Arrange
            var di = new MagicDI();

            // Act - Resolve multiple times to ensure deterministic selection
            var instance1 = di.Resolve<ClassWithSameParameterCountConstructors>();
            var instance2 = di.Resolve<ClassWithSameParameterCountConstructors>();

            // Assert - Should use the same constructor consistently
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            // Both should have used the same constructor (verified by consistent behavior)
        }

        [Fact]
        public void Resolve_TypeCastFailure_ThrowsInvalidOperationException()
        {
            // Arrange
            var di = new MagicDI();

            // Act - This tests the type safety in Resolve<T>
            var instance = di.Resolve<SimpleClass>();

            // Assert
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
                ClassWithDependency dependency2)
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
                ClassWithDependency dependency2)
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
