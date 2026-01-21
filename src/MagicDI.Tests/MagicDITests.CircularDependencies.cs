using System;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class CircularDependency
        {

            [Fact]
            public void ThrowsWhenDirectCircularDependencyDetected()
            {
                var di = new MagicDI();

                var act = () => di.Resolve<CircularA>();

                act.Should().Throw<InvalidOperationException>("direct circular dependencies must be detected to prevent stack overflow")
                    .WithMessage("*circular*", "the error message should clearly indicate a circular dependency was found");
            }

            #region Issue 3: Circular Dependency Detection

            [Fact]
            public void ThrowsDescriptiveExceptionForDirectCircle()
            {
                var di = new MagicDI();

                var act = () => di.Resolve<CircularClassA>();

                act.Should().Throw<InvalidOperationException>("A->B->A cycles must be detected before causing infinite recursion")
                    .WithMessage("*circular*", "the exception message should mention 'circular' to help developers diagnose the issue");
            }

            [Fact]
            public void DetectsIndirectCircularDependencies()
            {
                var di = new MagicDI();

                var act = () => di.Resolve<IndirectCircularA>();

                act.Should().Throw<InvalidOperationException>("indirect cycles like A->B->C->A must be detected regardless of chain length")
                    .WithMessage("*circular*", "even multi-hop cycles should produce a clear circular dependency message");
            }

            [Fact]
            public void DetectsSelfReferencingTypes()
            {
                var di = new MagicDI();

                var act = () => di.Resolve<SelfReferencingClass>();

                act.Should().Throw<InvalidOperationException>("a type depending on itself is the simplest form of circular dependency")
                    .WithMessage("*circular*", "self-references should be reported as circular dependencies");
            }

            [Fact]
            public void IncludesDependencyChainInExceptionMessage()
            {
                var di = new MagicDI();

                var act = () => di.Resolve<IndirectCircularA>();

                act.Should().Throw<InvalidOperationException>()
                    .WithMessage("*IndirectCircularA*", "the exception message should include the type names involved to help developers locate the cycle");
            }

            [Fact]
            public void RemainsUsableAfterCircularDependencyDetection()
            {
                var di = new MagicDI();

                var failedResolution = () => di.Resolve<CircularClassA>();
                failedResolution.Should().Throw<InvalidOperationException>("circular dependency should be detected");

                var instance = di.Resolve<NonCircularClass>();
                instance.Should().NotBeNull("the container should recover gracefully and continue resolving valid types after detecting a circular dependency");
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
