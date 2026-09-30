using HealthTrace.BLL.Exceptions;

namespace HealthTrace.Test.BLL.Exceptions
{
    /// <summary>
    /// Covers UnauthorizedException, ConflictException and BadRequestException together: they share
    /// the same contract (default message, custom message, inner exception) and have no
    /// payload beyond the text, so a Theory avoids three classes with three identical tests.
    /// No mocks: these types have no dependencies.
    /// Default messages matter more than they seem: they reach the client in the Detail of the
    /// response body, so changing them changes the API contract, not an internal detail.
    /// </summary>
    public class SimpleAppExceptionsTests
    {
        [Theory]
        [InlineData(typeof(UnauthorizedException), "Unauthorized.")]
        [InlineData(typeof(ConflictException), "Conflict.")]
        [InlineData(typeof(BadRequestException), "Bad request.")]
        public void DefaultMessage_IsTheTextTheClientReceives(Type exceptionType, string expected)
        {
            var exception = (Exception)Activator.CreateInstance(exceptionType)!;

            Assert.Equal(expected, exception.Message);
            Assert.Null(exception.InnerException);
        }

        [Theory]
        [InlineData(typeof(UnauthorizedException))]
        [InlineData(typeof(ConflictException))]
        [InlineData(typeof(BadRequestException))]
        public void CustomMessage_OverridesTheDefault(Type exceptionType)
        {
            const string custom = "Custom message raised by the test";

            var exception = (Exception)Activator.CreateInstance(exceptionType, custom)!;

            Assert.Equal(custom, exception.Message);
        }

        [Theory]
        [InlineData(typeof(UnauthorizedException))]
        [InlineData(typeof(ConflictException))]
        [InlineData(typeof(BadRequestException))]
        public void MessageAndInner_AreBothPreserved(Type exceptionType)
        {
            const string custom = "Custom message raised by the test";
            var inner = new InvalidOperationException("Test inner exception");

            var exception = (Exception)Activator.CreateInstance(exceptionType, custom, inner)!;

            Assert.Equal(custom, exception.Message);
            Assert.Same(inner, exception.InnerException);
        }
    }
}
