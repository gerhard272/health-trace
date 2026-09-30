using HealthTrace.BLL.Exceptions;

namespace HealthTrace.Test.BLL.Exceptions
{
    /// <summary>
    /// Covers ValidationException, the only exception that carries a payload besides the message: the
    /// field -> errors dictionary that becomes the 400 body. No mocks: a type without dependencies.
    /// The message matters as much as the payload, because it ends up in the response Detail: the
    /// constructors that do not receive one enforce "Validation failed.".
    /// Note: FluentValidation is not imported, so ValidationException resolves unambiguously
    /// to the BLL class, as described in the note in the production files.
    /// </summary>
    public class ValidationExceptionTests
    {
        private const string DefaultMessage = "Validation failed.";
        private const string FieldKey = "cf";
        private const string FieldMessage = "Format invalid";
        private const string GeneralKey = "general";
        private const string GeneralMessage = "Username already in use";

        [Fact]
        public void Parameterless_UsesDefaultMessageAndEmptyErrors()
        {
            // It can be thrown without any field error: Errors stays empty, not null, otherwise
            // the client would receive a body without the expected structure.
            var exception = new ValidationException();

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.NotNull(exception.Errors);
            Assert.Empty(exception.Errors);
        }

        [Fact]
        public void MessageOverload_KeepsCustomMessageAndEmptyErrors()
        {
            var exception = new ValidationException(DefaultMessage);

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.Empty(exception.Errors);
        }

        [Fact]
        public void MessageAndInnerOverload_KeepsMessageAndInnerAndEmptyErrors()
        {
            var inner = new InvalidOperationException("Inner failure raised by the test");

            var exception = new ValidationException(DefaultMessage, inner);

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.Same(inner, exception.InnerException);
            Assert.Empty(exception.Errors);
        }

        [Fact]
        public void ErrorsDictionary_CopiesFieldsAndKeepsDefaultMessage()
        {
            var errors = new Dictionary<string, string[]> { [FieldKey] = [FieldMessage] };

            var exception = new ValidationException(errors);

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.Equal(new[] { FieldMessage }, exception.Errors[FieldKey]);
        }

        [Fact]
        public void ErrorsDictionary_IsNotAffectedByLaterChangesToTheSource()
        {
            // The dictionary is recreated in the constructor: the service that built the
            // ValidationFailureResult can keep using its collection without changing
            // the exception that is about to cross the layers.
            var errors = new Dictionary<string, string[]> { [FieldKey] = [FieldMessage] };

            var exception = new ValidationException(errors);
            errors[FieldKey] = ["changed after construction"];
            errors["other"] = ["new field"];

            Assert.Equal(new[] { FieldMessage }, exception.Errors[FieldKey]);
            Assert.Single(exception.Errors);
        }

        [Fact]
        public void ErrorsDictionary_SharesTheErrorArraysByReference()
        {
            // Known limitation of the copy, which is shallow: the dictionary is recreated but the message
            // arrays stay shared, so a replacement in the source array is visible
            // in the exception. The test documents the behavior, it does not endorse it.
            var messages = new[] { FieldMessage };
            var exception = new ValidationException(new Dictionary<string, string[]> { [FieldKey] = messages });

            messages[0] = "changed";

            Assert.Equal("changed", exception.Errors[FieldKey][0]);
        }

        [Theory]
        [InlineData(FieldKey, FieldMessage)]
        [InlineData(GeneralKey, GeneralMessage)]
        public void FieldAndErrorOverload_KeepsFieldAndMessageVerbatim(string field, string error)
        {
            // Also used for errors without a field of their own, under the "general" key: the key must
            // reach the client as is, otherwise the frontend does not know where to show the error.
            var exception = new ValidationException(field, error);

            Assert.Equal(DefaultMessage, exception.Message);
            Assert.Equal(new[] { error }, exception.Errors[field]);
            Assert.Single(exception.Errors);
        }
    }
}
