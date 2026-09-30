using HealthTrace.BLL.Exceptions;

namespace HealthTrace.Test.BLL.Exceptions
{
    /// <summary>
    /// Covers the contract of the AppException base class, from which all application
    /// exceptions derive. No mocks: these types have no dependencies and are built directly.
    /// The hierarchy is not an internal detail: GlobalExceptionHandler and ExceptionStatusMapper
    /// choose the status code and response body based on it, so an exception that
    /// stopped deriving from AppException would end up in the 500 branch instead of the 4xx ones.
    /// </summary>
    public class AppExceptionTests
    {
        [Fact]
        public void AppException_IsAbstract()
        {
            // Nobody can throw a generic AppException: the handler branch that catches it
            // must be able to rely on the details (message, payload) provided by the derived types.
            Assert.True(typeof(AppException).IsAbstract);
        }

        [Theory]
        [InlineData(typeof(ValidationException))]
        [InlineData(typeof(UnauthorizedException))]
        [InlineData(typeof(NotFoundException))]
        [InlineData(typeof(ConflictException))]
        [InlineData(typeof(BadRequestException))]
        public void DerivedExceptions_AreCatchableAsAppException(Type exceptionType)
        {
            // All five application exceptions must stay catchable by the generic branch of the
            // handler; the service tests already cover the types actually thrown.
            var exception = (Exception)Activator.CreateInstance(exceptionType)!;

            Assert.IsAssignableFrom<AppException>(exception);
        }
    }
}
