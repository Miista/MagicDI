using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

// ReSharper disable ClassNeverInstantiated.Global

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class ErrorRecovery
        {
            public class ConstructorExceptionRecovery
            {
                [Fact]
                public void Container_remains_usable_after_constructor_throws()
                {
                    // Arrange
                    var di = new MagicDI();
                    Action fail = () => di.Resolve<ClassWithThrowingDep>();

                    // Act - first resolution fails
                    fail.Should().Throw<TargetInvocationException>();

                    // Assert - container still works for other types
                    var instance = di.Resolve<UnrelatedClass>();
                    instance.Should().NotBeNull(
                        because: "the container should remain usable after a constructor exception");
                }

                [Fact]
                public void Can_resolve_sibling_dependencies_after_failure()
                {
                    // Arrange
                    var di = new MagicDI();
                    Action fail = () => di.Resolve<ClassWithThrowingDep>();

                    // Act - first resolution fails
                    fail.Should().Throw<TargetInvocationException>();

                    // Assert - can resolve a type that shares some dependencies
                    var instance = di.Resolve<SiblingOfThrowingDep>();
                    instance.Should().NotBeNull(
                        because: "sibling types should be resolvable after a failure");
                    instance.Shared.Should().NotBeNull(
                        because: "shared dependencies should still work");
                }

                public class ThrowingConstructor
                {
                    public ThrowingConstructor()
                    {
                        throw new InvalidOperationException("Constructor intentionally throws");
                    }
                }

                public class SharedDependency;

                public class ClassWithThrowingDep(ThrowingConstructor throwing)
                {
                    public ThrowingConstructor Throwing { get; } = throwing;
                }

                public class SiblingOfThrowingDep(SharedDependency shared)
                {
                    public SharedDependency Shared { get; } = shared;
                }

                public class UnrelatedClass;
            }

            public class CaptiveDependencyRecovery
            {
                [Fact]
                public void Container_remains_usable_after_captive_error()
                {
                    // Arrange
                    var di = new MagicDI();
                    Action fail = () => di.Resolve<CaptiveSingleton>();

                    // Act - captive dependency error
                    fail.Should().Throw<InvalidOperationException>()
                        .WithMessage("*Captive*");

                    // Assert - container still works
                    var instance = di.Resolve<SafeSingleton>();
                    instance.Should().NotBeNull(
                        because: "the container should remain usable after a captive dependency error");
                }

                public class TransientDep : IDisposable
                {
                    public void Dispose() { }
                }

                [Lifetime(Lifetime.Singleton)]
                public class CaptiveSingleton(TransientDep dep)
                {
                    public TransientDep Dep { get; } = dep;
                }

                public class SafeSingleton;
            }

            public class InterfaceResolutionRecovery
            {
                [Fact]
                public void Container_remains_usable_after_no_implementation_found()
                {
                    // Arrange
                    var di = new MagicDI();
                    Action fail = () => di.Resolve<INoImplementation>();

                    // Act - no implementation error
                    fail.Should().Throw<InvalidOperationException>()
                        .WithMessage("*No implementation*");

                    // Assert - container still works
                    var instance = di.Resolve<ConcreteClass>();
                    instance.Should().NotBeNull(
                        because: "the container should remain usable after interface resolution failure");
                }

                public interface INoImplementation { }

                public class ConcreteClass;
            }

            public class DeepNesting
            {
                [Fact]
                public void Resolves_fifty_level_deep_dependency_chain()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<Deep50>();

                    // Assert
                    instance.Should().NotBeNull(
                        because: "the container should handle very deep dependency chains");
                }

                [Fact]
                public void Recovers_from_exception_at_depth()
                {
                    // Arrange
                    var di = new MagicDI();
                    Action fail = () => di.Resolve<DeepWithThrow>();

                    // Act - exception deep in the chain
                    fail.Should().Throw<TargetInvocationException>();

                    // Assert - container still works
                    var instance = di.Resolve<Deep50>();
                    instance.Should().NotBeNull(
                        because: "the container should recover from exceptions in deep chains");
                }

                // 50-level deep chain
                public class Deep01;
                public class Deep02(Deep01 d) { public Deep01 D { get; } = d; }
                public class Deep03(Deep02 d) { public Deep02 D { get; } = d; }
                public class Deep04(Deep03 d) { public Deep03 D { get; } = d; }
                public class Deep05(Deep04 d) { public Deep04 D { get; } = d; }
                public class Deep06(Deep05 d) { public Deep05 D { get; } = d; }
                public class Deep07(Deep06 d) { public Deep06 D { get; } = d; }
                public class Deep08(Deep07 d) { public Deep07 D { get; } = d; }
                public class Deep09(Deep08 d) { public Deep08 D { get; } = d; }
                public class Deep10(Deep09 d) { public Deep09 D { get; } = d; }
                public class Deep11(Deep10 d) { public Deep10 D { get; } = d; }
                public class Deep12(Deep11 d) { public Deep11 D { get; } = d; }
                public class Deep13(Deep12 d) { public Deep12 D { get; } = d; }
                public class Deep14(Deep13 d) { public Deep13 D { get; } = d; }
                public class Deep15(Deep14 d) { public Deep14 D { get; } = d; }
                public class Deep16(Deep15 d) { public Deep15 D { get; } = d; }
                public class Deep17(Deep16 d) { public Deep16 D { get; } = d; }
                public class Deep18(Deep17 d) { public Deep17 D { get; } = d; }
                public class Deep19(Deep18 d) { public Deep18 D { get; } = d; }
                public class Deep20(Deep19 d) { public Deep19 D { get; } = d; }
                public class Deep21(Deep20 d) { public Deep20 D { get; } = d; }
                public class Deep22(Deep21 d) { public Deep21 D { get; } = d; }
                public class Deep23(Deep22 d) { public Deep22 D { get; } = d; }
                public class Deep24(Deep23 d) { public Deep23 D { get; } = d; }
                public class Deep25(Deep24 d) { public Deep24 D { get; } = d; }
                public class Deep26(Deep25 d) { public Deep25 D { get; } = d; }
                public class Deep27(Deep26 d) { public Deep26 D { get; } = d; }
                public class Deep28(Deep27 d) { public Deep27 D { get; } = d; }
                public class Deep29(Deep28 d) { public Deep28 D { get; } = d; }
                public class Deep30(Deep29 d) { public Deep29 D { get; } = d; }
                public class Deep31(Deep30 d) { public Deep30 D { get; } = d; }
                public class Deep32(Deep31 d) { public Deep31 D { get; } = d; }
                public class Deep33(Deep32 d) { public Deep32 D { get; } = d; }
                public class Deep34(Deep33 d) { public Deep33 D { get; } = d; }
                public class Deep35(Deep34 d) { public Deep34 D { get; } = d; }
                public class Deep36(Deep35 d) { public Deep35 D { get; } = d; }
                public class Deep37(Deep36 d) { public Deep36 D { get; } = d; }
                public class Deep38(Deep37 d) { public Deep37 D { get; } = d; }
                public class Deep39(Deep38 d) { public Deep38 D { get; } = d; }
                public class Deep40(Deep39 d) { public Deep39 D { get; } = d; }
                public class Deep41(Deep40 d) { public Deep40 D { get; } = d; }
                public class Deep42(Deep41 d) { public Deep41 D { get; } = d; }
                public class Deep43(Deep42 d) { public Deep42 D { get; } = d; }
                public class Deep44(Deep43 d) { public Deep43 D { get; } = d; }
                public class Deep45(Deep44 d) { public Deep44 D { get; } = d; }
                public class Deep46(Deep45 d) { public Deep45 D { get; } = d; }
                public class Deep47(Deep46 d) { public Deep46 D { get; } = d; }
                public class Deep48(Deep47 d) { public Deep47 D { get; } = d; }
                public class Deep49(Deep48 d) { public Deep48 D { get; } = d; }
                public class Deep50(Deep49 d) { public Deep49 D { get; } = d; }

                // Chain that throws at depth
                public class ThrowAtDepth
                {
                    public ThrowAtDepth()
                    {
                        throw new InvalidOperationException("Throws at depth");
                    }
                }

                public class DeepWithThrow(ThrowAtDepth t)
                {
                    public ThrowAtDepth T { get; } = t;
                }
            }

            public class MultipleExceptionsInSequence
            {
                [Fact]
                public void Container_remains_usable_after_multiple_failures()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - multiple different failures
                    Action fail1 = () => di.Resolve<INoImpl>();
                    Action fail2 = () => di.Resolve<CaptiveBad>();
                    Action fail3 = () => di.Resolve<Throws>();

                    fail1.Should().Throw<InvalidOperationException>();
                    fail2.Should().Throw<InvalidOperationException>();
                    fail3.Should().Throw<TargetInvocationException>();

                    // Assert - container still works after all failures
                    var instance = di.Resolve<SafeClass>();
                    instance.Should().NotBeNull(
                        because: "the container should remain usable after multiple different failures");
                }

                public interface INoImpl { }

                public class TransientClass : IDisposable
                {
                    public void Dispose() { }
                }

                [Lifetime(Lifetime.Singleton)]
                public class CaptiveBad(TransientClass t)
                {
                    public TransientClass T { get; } = t;
                }

                public class Throws
                {
                    public Throws()
                    {
                        throw new InvalidOperationException("Throws");
                    }
                }

                public class SafeClass;
            }
        }
    }
}
