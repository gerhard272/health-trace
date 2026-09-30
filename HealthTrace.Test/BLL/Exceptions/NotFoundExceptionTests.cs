using HealthTrace.BLL.Exceptions;

namespace HealthTrace.Test.BLL.Exceptions
{
    /// <summary>
    /// Covers NotFoundException, the only exception that also keeps the data used to build the
    /// message: ResourceName and Key feed the extensions of the 404 returned by the handler.
    /// No mocks: a type without dependencies.
    /// The message is built here and not in the handler, so this is where it is decided whether
    /// a lookup without a key produces "resource not found" or a message with an empty key:
    /// the two cases must stay distinguishable.
    /// </summary>
    public class NotFoundExceptionTests
    {
        private const string ResourceName = "Symptom";

        [Fact]
        public void WithKey_BuildsMessageWithKeyAndKeepsItOnTheException()
        {
            var exception = new NotFoundException(ResourceName, 99);

            Assert.Equal("Symptom with key '99' was not found.", exception.Message);
            Assert.Equal(ResourceName, exception.ResourceName);
            Assert.Equal(99, exception.Key);
        }

        [Fact]
        public void WithoutKey_BuildsMessageWithoutKey()
        {
            // Lookup by a non-identifying criterion: the message must not contain a key,
            // and Key stays null so the handler omits the resourceKey extension.
            var exception = new NotFoundException(ResourceName);

            Assert.Equal("Symptom not found.", exception.Message);
            Assert.Equal(ResourceName, exception.ResourceName);
            Assert.Null(exception.Key);
        }

        [Fact]
        public void WithEmptyKey_BuildsMessageWithEmptyKey()
        {
            // Key present but empty: a different case from a lookup without a key, so the
            // message tells it apart instead of repeating "not found" as if the key were missing.
            var exception = new NotFoundException(ResourceName, string.Empty);

            Assert.Equal("Symptom with key '' was not found.", exception.Message);
            Assert.Equal(string.Empty, exception.Key);
        }

        [Fact]
        public void Parameterless_UsesGenericResourceNameAndNoKey()
        {
            var exception = new NotFoundException();

            Assert.Equal("Resource not found.", exception.Message);
            Assert.Equal("Resource", exception.ResourceName);
            Assert.Null(exception.Key);
        }

        [Theory]
        [InlineData(typeof(int), 99)]
        [InlineData(typeof(string), "abc")]
        public void Key_IsKeptWithItsOriginalRuntimeType(Type expectedType, object key)
        {
            // The exception keeps the object as it is: converting it to text is the handler's job,
            // done on the body extension. Here we check that the type is not lost, so the handler
            // stays the only place where the rendering is decided.
            var exception = new NotFoundException(ResourceName, key);

            Assert.IsType(expectedType, exception.Key);
            Assert.Equal(key, exception.Key);
        }

        [Fact]
        public void WithInnerException_KeepsTheKeyAndTheInnerException()
        {
            var inner = new InvalidOperationException("Inner failure raised by the test");

            var exception = new NotFoundException(ResourceName, 99, inner);

            Assert.Equal("Symptom with key '99' was not found.", exception.Message);
            Assert.Equal(99, exception.Key);
            Assert.Same(inner, exception.InnerException);
        }
    }
}
