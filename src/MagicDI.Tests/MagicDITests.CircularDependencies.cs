using System;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class CircularDependency
        {

            [Fact]
            public void Resolve_CircularDependency_ThrowsInvalidOperationException()
            {
                // Arrange
                var di = new MagicDI();

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => di.Resolve<CircularA>());
                Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            #region Issue 3: Circular Dependency Detection

            /// <summary>
            /// Reveals: Circular dependency should throw descriptive exception,
            /// not cause StackOverflowException.
            /// Note: This test cannot actually run the circular resolution because
            /// StackOverflowException terminates the process.
            /// </summary>
            [Fact]
            public void CircularDependency_DirectCircle_ShouldThrowDescriptiveException()
            {
                // Arrange
                var di = new MagicDI();

                // Act & Assert
                // Should throw InvalidOperationException with message about circular dependency
                // Currently causes StackOverflowException which crashes the test process
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    di.Resolve<CircularClassA>()
                );

                Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Reveals: Indirect circular dependency (A -> B -> C -> A) should be detected.
            /// </summary>
            [Fact]
            public void CircularDependency_IndirectCircle_ShouldThrowDescriptiveException()
            {
                // Arrange
                var di = new MagicDI();

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    di.Resolve<IndirectCircularA>()
                );

                Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Reveals: Self-referencing type should be detected as circular.
            /// </summary>
            [Fact]
            public void CircularDependency_SelfReference_ShouldThrowDescriptiveException()
            {
                // Arrange
                var di = new MagicDI();

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    di.Resolve<SelfReferencingClass>()
                );

                Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Reveals: The exception message should include the dependency chain
            /// to help developers identify where the cycle occurs.
            /// </summary>
            [Fact]
            public void CircularDependency_ExceptionMessage_ShouldIncludeDependencyChain()
            {
                // Arrange
                var di = new MagicDI();

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    di.Resolve<IndirectCircularA>()
                );

                // Should mention the types involved in the cycle
                Assert.Contains("IndirectCircularA", exception.Message);
            }

            /// <summary>
            /// Reveals: After detecting circular dependency in one resolution,
            /// subsequent resolutions of non-circular types should still work.
            /// </summary>
            [Fact]
            public void CircularDependency_AfterDetection_ContainerShouldRemainUsable()
            {
                // Arrange
                var di = new MagicDI();

                // Act - Try to resolve circular dependency (should fail gracefully)
                Assert.Throws<InvalidOperationException>(() => di.Resolve<CircularClassA>());

                // Assert - Container should still work for valid types
                var instance = di.Resolve<NonCircularClass>();
                Assert.NotNull(instance);
            }

            #endregion

            #region Circular Dependency Test Classes

            public class CircularA
            {
                public CircularA(CircularB b) { }
            }

            public class CircularB
            {
                public CircularB(CircularA a) { }
            }

            public class CircularClassA
            {
                public CircularClassA(CircularClassB b) { }
            }

            public class CircularClassB
            {
                public CircularClassB(CircularClassA a) { }
            }

            public class IndirectCircularA
            {
                public IndirectCircularA(IndirectCircularB b) { }
            }

            public class IndirectCircularB
            {
                public IndirectCircularB(IndirectCircularC c) { }
            }

            public class IndirectCircularC
            {
                public IndirectCircularC(IndirectCircularA a) { }
            }

            public class SelfReferencingClass
            {
                public SelfReferencingClass(SelfReferencingClass self) { }
            }

            public class NonCircularClass { }

            #endregion
        }
    }
}
