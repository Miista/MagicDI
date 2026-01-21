using System;
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

                var exception = Assert.Throws<InvalidOperationException>(() => di.Resolve<CircularA>());
                Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            #region Issue 3: Circular Dependency Detection

            [Fact]
            public void ThrowsDescriptiveExceptionForDirectCircle()
            {
                var di = new MagicDI();

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    di.Resolve<CircularClassA>()
                );

                Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            [Fact]
            public void DetectsIndirectCircularDependencies()
            {
                var di = new MagicDI();

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    di.Resolve<IndirectCircularA>()
                );

                Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            [Fact]
            public void DetectsSelfReferencingTypes()
            {
                var di = new MagicDI();

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    di.Resolve<SelfReferencingClass>()
                );

                Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            [Fact]
            public void IncludesDependencyChainInExceptionMessage()
            {
                var di = new MagicDI();

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    di.Resolve<IndirectCircularA>()
                );

                Assert.Contains("IndirectCircularA", exception.Message);
            }

            [Fact]
            public void RemainsUsableAfterCircularDependencyDetection()
            {
                var di = new MagicDI();

                Assert.Throws<InvalidOperationException>(() => di.Resolve<CircularClassA>());

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
