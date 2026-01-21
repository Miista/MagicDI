using System;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
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

        #region Circular Dependency Test Classes

        public class CircularA
        {
            public CircularA(CircularB b) { }
        }

        public class CircularB
        {
            public CircularB(CircularA a) { }
        }

        #endregion
    }
}
