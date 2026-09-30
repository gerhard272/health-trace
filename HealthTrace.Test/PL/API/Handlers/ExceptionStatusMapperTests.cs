using HealthTrace.BLL.Exceptions;
// This file's namespace mirrors that of the code under test, so the
// nested namespace resolution rule does not find the mapper on its own: the
// explicit declaration is needed, as everywhere else where the two
// namespaces share the same last segment.
using HealthTrace.PL.API.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AppValidationException = HealthTrace.BLL.Exceptions.ValidationException;

namespace HealthTrace.Test.PL.API.Handlers
{
    /// <summary>
    /// Test class for ExceptionStatusMapper, the only place in the application where
    /// an exception type is translated into an HTTP status code and a ProblemDetails
    /// body. No mocks or host needed: the mapper is pure, has no
    /// dependencies and is instantiated directly, so every assertion is about a real
    /// branch of the switch.
    /// The tests check the translation, not the text of the individual exceptions: the
    /// Detail is always the message of the received exception, built by the exception
    /// itself, while the choice of status, title, problem type and extensions is
    /// the mapper's responsibility. That is why messages are compared with
    /// exception.Message and not written out.
    /// The 500 branch is the only one that receives no details: explicitly asserting
    /// that Detail stays null blocks the disclosure of internal information, which
    /// the class doc comment describes as the intended behavior.
    /// </summary>
    public class ExceptionStatusMapperTests
    {
        private const string ResourceName = "Symptom";
        private const int ResourceKey = 99;

        private const string ValidationTitle = "Validation failed";
        private const string UnauthorizedTitle = "Unauthorized";
        private const string NotFoundTitle = "Not Found";
        private const string ConflictTitle = "Conflict";
        private const string BadRequestTitle = "Bad Request";
        private const string InternalServerErrorTitle = "Internal Server Error";

        private const string ResourceNameExtension = "resourceName";
        private const string ResourceKeyExtension = "resourceKey";

        private readonly ExceptionStatusMapper _mapper = new();

        // --- ValidationException ---

        [Fact]
        public void Map_ValidationException_ReturnsValidationProblemDetailsWithFieldErrors()
        {
            var errors = new Dictionary<string, string[]>
            {
                ["username"] = ["Username already in use"],
                ["cf"] = ["Fiscal code already registered", "Format invalid"]
            };

            var problem = _mapper.Map(new AppValidationException(errors));

            // A concrete type other than ProblemDetails: it is the signal the client uses
            // to know that errors are grouped by field name.
            var validation = Assert.IsType<ValidationProblemDetails>(problem);
            Assert.Equal(StatusCodes.Status400BadRequest, validation.Status);
            Assert.Equal(ValidationTitle, validation.Title);
            Assert.Equal("Validation failed.", validation.Detail);

            // Grouping by field is what this branch is about: the same
            // key keeps several messages, different keys stay separate.
            Assert.Equal(2, validation.Errors.Count);
            Assert.Equal(new[] { "Username already in use" }, validation.Errors["username"]);
            Assert.Equal(
                new[] { "Fiscal code already registered", "Format invalid" },
                validation.Errors["cf"]);
        }

        [Fact]
        public void Map_ValidationException_WithoutFieldErrors_ReturnsEmptyErrorDictionary()
        {
            // The exception can be thrown without any field error: the mapper
            // must still return an empty dictionary, not a null reference.
            var problem = _mapper.Map(new AppValidationException());

            var validation = Assert.IsType<ValidationProblemDetails>(problem);
            Assert.Equal(StatusCodes.Status400BadRequest, validation.Status);
            Assert.Empty(validation.Errors);
        }

        // --- UnauthorizedException ---

        [Fact]
        public void Map_UnauthorizedException_Returns401WithExceptionMessageAsDetail()
        {
            // A single message for non-existent and wrong credentials: a
            // security choice to prevent account enumeration.
            var exception = new UnauthorizedException("Invalid username or password");

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
            Assert.Equal(UnauthorizedTitle, problem.Title);
            Assert.Equal(exception.Message, problem.Detail);
            Assert.Empty(problem.Extensions);
        }

        // --- NotFoundException ---

        [Fact]
        public void Map_NotFoundException_Returns404WithResourceExtensions()
        {
            var exception = new NotFoundException(ResourceName, ResourceKey);

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal(NotFoundTitle, problem.Title);
            Assert.Equal(exception.Message, problem.Detail);

            // The extensions are information the client did not get before the
            // move to exceptions: they say which resource and which key were looked up.
            Assert.Equal(ResourceName, Assert.IsType<string>(problem.Extensions[ResourceNameExtension]));
            Assert.Equal("99", Assert.IsType<string>(problem.Extensions[ResourceKeyExtension]));
        }

        [Fact]
        public void Map_NotFoundException_WithoutKey_OmitsResourceKeyExtension()
        {
            // Without a key no empty extension is emitted: the client must be able to
            // tell "looked up without a key" apart from "looked up with an empty key".
            var problem = _mapper.Map(new NotFoundException(ResourceName));

            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal(ResourceName, Assert.IsType<string>(problem.Extensions[ResourceNameExtension]));
            Assert.False(problem.Extensions.ContainsKey(ResourceKeyExtension));
        }

        [Theory]
        [InlineData(99, "99")]
        [InlineData("abc", "abc")]
        public void Map_NotFoundException_ConvertsKeyToText(object key, string expected)
        {
            // The key arrives as an object and must travel as text in the body.
            var problem = _mapper.Map(new NotFoundException(ResourceName, key));

            Assert.Equal(expected, Assert.IsType<string>(problem.Extensions[ResourceKeyExtension]));
        }

        // --- ConflictException ---

        [Fact]
        public void Map_ConflictException_Returns409WithExceptionMessageAsDetail()
        {
            var exception = new ConflictException("The symptom is already under analysis.");

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
            Assert.Equal(ConflictTitle, problem.Title);
            Assert.Equal(exception.Message, problem.Detail);
        }

        // --- Other AppException ---

        [Fact]
        public void Map_OtherAppException_Returns400WithBadRequestTitle()
        {
            // BadRequestException derives from AppException like the previous types, but
            // none of the specific branches catches it: this proves the specific branches
            // are evaluated before the generic one and are not swallowed by it.
            var exception = new BadRequestException("The uploaded file is too large.");

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
            Assert.Equal(BadRequestTitle, problem.Title);
            Assert.NotEqual(ValidationTitle, problem.Title);
            Assert.Equal(exception.Message, problem.Detail);
        }

        // --- Unexpected exception ---

        [Theory]
        [InlineData(typeof(FormatException))]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(ArgumentNullException))]
        [InlineData(typeof(Exception))]
        public void Map_UnexpectedException_Returns500WithoutDetail(Type exceptionType)
        {
            // None of these exceptions is known to the mapper, so the final branch
            // must produce the same 500 for all of them.
            var exception = (Exception)Activator.CreateInstance(exceptionType)!;

            var problem = _mapper.Map(exception);

            Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
            Assert.Equal(InternalServerErrorTitle, problem.Title);

            // No details exposed: the body stays generic and the details stay
            // in the log, with the traceId as correlation key.
            Assert.Null(problem.Detail);
            Assert.Empty(problem.Extensions);
        }
    }
}
