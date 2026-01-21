using System;
using FluentAssertions;
using Xunit;

// ReSharper disable UnusedParameter.Local
// ReSharper disable ClassNeverInstantiated.Global

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class CircularDependency
        {
            [Fact]
            public void Throws_when_direct_circular_dependency_detected()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action act = () => di.Resolve<CircularA>();

                // Assert
                act.Should().Throw<InvalidOperationException>(because: "direct circular dependencies must be detected to prevent stack overflow")
                    .WithMessage("*circular*", because: "the error message should clearly indicate a circular dependency was found");
            }

            #region Issue 3: Circular Dependency Detection

            [Fact]
            public void Throws_descriptive_exception_for_direct_circle()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action act = () => di.Resolve<CircularClassA>();

                // Assert
                act.Should().Throw<InvalidOperationException>(because: "A->B->A cycles must be detected before causing infinite recursion")
                    .WithMessage("*circular*", because: "the exception message should mention 'circular' to help developers diagnose the issue");
            }

            [Fact]
            public void Detects_indirect_circular_dependencies()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action act = () => di.Resolve<IndirectCircularA>();

                // Assert
                act.Should().Throw<InvalidOperationException>(because: "indirect cycles like A->B->C->A must be detected regardless of chain length")
                    .WithMessage("*circular*", because: "even multi-hop cycles should produce a clear circular dependency message");
            }

            [Fact]
            public void Detects_self_referencing_types()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action act = () => di.Resolve<SelfReferencingClass>();

                // Assert
                act.Should().Throw<InvalidOperationException>(because: "a type depending on itself is the simplest form of circular dependency")
                    .WithMessage("*circular*", because: "self-references should be reported as circular dependencies");
            }

            [Fact]
            public void Includes_dependency_chain_in_exception_message()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action act = () => di.Resolve<IndirectCircularA>();

                // Assert
                act.Should().Throw<InvalidOperationException>()
                    .WithMessage("*IndirectCircularA*", because: "the exception message should include the type names involved to help developers locate the cycle");
            }

            [Fact]
            public void Remains_usable_after_circular_dependency_detection()
            {
                // Arrange
                var di = new MagicDI();

                // Act
                Action failedResolution = () => di.Resolve<CircularClassA>();
                failedResolution.Should().Throw<InvalidOperationException>(because: "circular dependency should be detected");

                var instance = di.Resolve<NonCircularClass>();

                // Assert
                instance.Should().NotBeNull(because: "the container should recover gracefully and continue resolving valid types after detecting a circular dependency");
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
