using HealthTrace.BLL.Exceptions;
using HealthTrace.PL.API.Handlers;
using AppValidationException = HealthTrace.BLL.Exceptions.ValidationException;

namespace HealthTrace.Test.PL.API.Handlers
{
    public class ExceptionLogPropertiesTests
    {
        private const string ResourceName = "Symptom";
        private const int ResourceKey = 99;

        [Fact]
        public void For_NotFoundException_ReturnsErrorTypeResourceNameAndKey()
        {
            var properties = ExceptionLogProperties.For(new NotFoundException(ResourceName, ResourceKey));

            Assert.Equal("NotFoundException", properties["ErrorType"]);
            Assert.Equal(ResourceName, properties["ResourceName"]);
            Assert.Equal(ResourceKey, Assert.IsType<int>(properties["ResourceKey"]));
        }

        [Fact]
        public void For_NotFoundException_WithoutKey_OmitsResourceKey()
        {
            var properties = ExceptionLogProperties.For(new NotFoundException(ResourceName));

            Assert.Equal("NotFoundException", properties["ErrorType"]);
            Assert.Equal(ResourceName, properties["ResourceName"]);
            Assert.False(properties.ContainsKey("ResourceKey"));
        }

        [Theory]
        [InlineData(99, typeof(int))]
        [InlineData("abc", typeof(string))]
        [InlineData(true, typeof(bool))]
        [InlineData(1.5d, typeof(double))]
        public void For_NotFoundException_Key_KeepsOriginalTypeNotText(object key, Type expectedType)
        {
            var properties = ExceptionLogProperties.For(new NotFoundException(ResourceName, key));

            Assert.IsType(expectedType, properties["ResourceKey"]);
            Assert.Equal(key, properties["ResourceKey"]);
        }

        [Fact]
        public void For_DerivedNotFoundException_KeepsBranchAndUsesDerivedTypeName()
        {
            var properties = ExceptionLogProperties.For(new DerivedNotFoundException());

            Assert.Equal("DerivedNotFoundException", properties["ErrorType"]);
            Assert.Equal(ResourceName, properties["ResourceName"]);
            Assert.Equal(1, properties["ResourceKey"]);
            Assert.False(properties.ContainsKey("ErrorFields"));
        }

        [Fact]
        public void For_ValidationException_ReturnsFieldsAndErrorCount()
        {
            var errors = new Dictionary<string, string[]>
            {
                ["username"] = ["Username already in use"],
                ["cf"] = ["Fiscal code already registered", "Format invalid"]
            };

            var properties = ExceptionLogProperties.For(new AppValidationException(errors));

            Assert.Equal("ValidationException", properties["ErrorType"]);
            Assert.Equal(new[] { "username", "cf" }, Assert.IsType<string[]>(properties["ErrorFields"]));
            Assert.Equal(3, properties["ErrorCount"]);
        }

        [Fact]
        public void For_ValidationException_WithoutErrors_ReturnsEmptyFieldsAndZeroCount()
        {
            var properties = ExceptionLogProperties.For(new AppValidationException());

            Assert.Equal("ValidationException", properties["ErrorType"]);
            Assert.Empty(Assert.IsType<string[]>(properties["ErrorFields"]));
            Assert.Equal(0, properties["ErrorCount"]);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 2)]
        [InlineData(3, 3)]
        public void For_ValidationException_CountsAllErrorsAcrossFields(int fieldCount, int errorsPerField)
        {
            var errors = Enumerable.Range(0, fieldCount)
                .ToDictionary(index => $"field{index}", _ => new string[errorsPerField]);

            var properties = ExceptionLogProperties.For(new AppValidationException(errors));

            Assert.Equal(fieldCount, Assert.IsType<string[]>(properties["ErrorFields"]).Length);
            Assert.Equal(fieldCount * errorsPerField, properties["ErrorCount"]);
        }

        [Fact]
        public void For_ValidationException_WithNullErrorsArray_CountsZeroForThatField()
        {
            var errors = new Dictionary<string, string[]>
            {
                ["username"] = null!,
                ["cf"] = ["Fiscal code already registered", "Format invalid"]
            };

            var properties = ExceptionLogProperties.For(new AppValidationException(errors));

            Assert.Equal(new[] { "username", "cf" }, Assert.IsType<string[]>(properties["ErrorFields"]));
            Assert.Equal(2, properties["ErrorCount"]);
        }

        [Fact]
        public void For_ValidationException_ReturnsFieldNamesInDeclarationOrder()
        {
            var errors = new Dictionary<string, string[]>
            {
                ["zzz"] = ["First"],
                ["aaa"] = ["Second"]
            };

            var properties = ExceptionLogProperties.For(new AppValidationException(errors));

            Assert.Equal(new[] { "zzz", "aaa" }, Assert.IsType<string[]>(properties["ErrorFields"]));
        }

        [Theory]
        [InlineData(typeof(FormatException))]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(ArgumentNullException))]
        [InlineData(typeof(Exception))]
        public void For_UnexpectedException_ReturnsOnlyErrorType(Type exceptionType)
        {
            var exception = (Exception)Activator.CreateInstance(exceptionType)!;

            var properties = ExceptionLogProperties.For(exception);

            var only = Assert.Single(properties);
            Assert.Equal("ErrorType", only.Key);
            Assert.Equal(exceptionType.Name, only.Value);
        }

        [Theory]
        [InlineData(typeof(BadRequestException))]
        [InlineData(typeof(ConflictException))]
        [InlineData(typeof(UnauthorizedException))]
        public void For_OtherAppException_ReturnsOnlyErrorType(Type exceptionType)
        {
            var exception = (Exception)Activator.CreateInstance(exceptionType, "Unexpected failure")!;

            var properties = ExceptionLogProperties.For(exception);

            var only = Assert.Single(properties);
            Assert.Equal("ErrorType", only.Key);
            Assert.Equal(exceptionType.Name, only.Value);
        }

        [Fact]
        public void For_DoesNotAddPropertiesOfTheOtherBranch()
        {
            var errors = new Dictionary<string, string[]> { ["username"] = ["Username already in use"] };

            var notFound = ExceptionLogProperties.For(new NotFoundException(ResourceName, ResourceKey));
            var validation = ExceptionLogProperties.For(new AppValidationException(errors));

            Assert.Equal(new[] { "ErrorType", "ResourceName", "ResourceKey" }, notFound.Keys);
            Assert.Equal(new[] { "ErrorType", "ErrorFields", "ErrorCount" }, validation.Keys);
        }

        [Fact]
        public void For_UsesOrdinalKeyComparer()
        {
            var properties = ExceptionLogProperties.For(new NotFoundException(ResourceName, ResourceKey));

            Assert.True(properties.ContainsKey("ErrorType"));
            Assert.False(properties.ContainsKey("errortype"));
            Assert.False(properties.ContainsKey("ERRORTYPE"));
        }

        [Fact]
        public void For_ReturnsIndependentDictionaryEachCall()
        {
            var exception = new NotFoundException(ResourceName, ResourceKey);

            var first = (IDictionary<string, object?>)ExceptionLogProperties.For(exception);
            var second = (IDictionary<string, object?>)ExceptionLogProperties.For(exception);

            first["ErrorType"] = "Changed";

            Assert.NotSame(first, second);
            Assert.Equal("NotFoundException", second["ErrorType"]);
        }

        private sealed class DerivedNotFoundException : NotFoundException
        {
            public DerivedNotFoundException() : base("Symptom", 1) { }
        }
    }
}
