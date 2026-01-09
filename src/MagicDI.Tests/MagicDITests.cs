using System;
using Xunit;

namespace MagicDI.Tests
{
    public class MagicDITests
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

        #endregion
    }
}
