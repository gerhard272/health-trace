using FluentValidation;
using FluentValidation.Results;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Validations;

namespace HealthTrace.Test.BLL.Validations
{
    /// <summary>
    /// Test class for RegisterModelValidator, the only FluentValidation validator
    /// in the BLL. No mocks needed: the validator is pure, it is instantiated and invoked
    /// directly, so every assertion is about a real rule.
    /// Two framework details drive the expectations:
    /// - cascade Continue (default): a failing rule does not short-circuit the others
    ///   on the same property, so a field can produce more than one error;
    /// - length validators and Matches only apply to non-null strings, while
    ///   NotEmpty also treats a whitespace-only string as empty.
    /// Tests that pin more than one error document the actual behavior.
    /// </summary>
    public class RegisterModelValidatorTests
    {
        private const string ValidUsername = "mario.rossi";
        private const string ValidFirstName = "Mario";
        private const string ValidLastName = "Rossi";
        private const string ValidCf = "RSSMRA80A01H501U";
        private const string PlainPassword = "Password123!";

        private const int NameMaxLength = 50;
        private const int CfLength = 16;
        private const int PasswordMinLength = 8;
        private const int PasswordMaxLength = 72;

        private readonly RegisterModelValidator _validator = new();

        private static RegisterModel ValidModel() => new()
        {
            Username = ValidUsername,
            FirstName = ValidFirstName,
            LastName = ValidLastName,
            CF = ValidCf,
            Password = PlainPassword,
            PasswordConfirmation = PlainPassword,
            BirthDate = new DateOnly(1980, 1, 1)
        };

        private static string Repeat(char c, int length) => new(c, length);

        // Isolates the errors of a single property. Needed because the same anomaly
        // (missing password or confirmation) fails several rules, and the tests want
        // to assert on one field's contract without depending on the others.
        private static ValidationFailure[] Errors(ValidationResult result, string property) =>
            result.Errors.Where(e => e.PropertyName == property).ToArray();

        private static string[] Messages(IEnumerable<ValidationFailure> errors) =>
            errors.Select(e => e.ErrorMessage).ToArray();

        // --- Happy path ---

        [Fact]
        public void Validate_ValidModel_ReturnsNoErrors()
        {
            var result = _validator.Validate(ValidModel());

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        // --- Username ---

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Validate_EmptyUsername_ReturnsRequiredError(string? username)
        {
            var model = ValidModel();
            model.Username = username!;

            var result = _validator.Validate(model);

            // MaximumLength(50) adds no errors: it skips null, and an empty or
            // whitespace string is within the limit. Only the required rule remains.
            var error = Assert.Single(result.Errors);
            Assert.Equal("Username", error.PropertyName);
            Assert.Equal("Username is required", error.ErrorMessage);
        }

        [Fact]
        public void Validate_UsernameLongerThan50_ReturnsMaxLengthError()
        {
            var model = ValidModel();
            model.Username = Repeat('a', NameMaxLength + 1);

            var result = _validator.Validate(model);

            var error = Assert.Single(result.Errors);
            Assert.Equal("Username", error.PropertyName);
            Assert.Equal("Username must not exceed 50 characters", error.ErrorMessage);
        }

        [Fact]
        public void Validate_UsernameExactly50_IsAccepted()
        {
            var model = ValidModel();
            model.Username = Repeat('a', NameMaxLength);

            var result = _validator.Validate(model);

            // The limit is inclusive: only one more character would fail.
            Assert.Empty(result.Errors);
        }

        // --- FirstName ---

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Validate_EmptyFirstName_ReturnsRequiredError(string? firstName)
        {
            var model = ValidModel();
            model.FirstName = firstName!;

            var result = _validator.Validate(model);

            var error = Assert.Single(result.Errors);
            Assert.Equal("FirstName", error.PropertyName);
            Assert.Equal("FirstName is required", error.ErrorMessage);
        }

        [Fact]
        public void Validate_FirstNameLongerThan50_ReturnsMaxLengthError()
        {
            var model = ValidModel();
            model.FirstName = Repeat('a', NameMaxLength + 1);

            var result = _validator.Validate(model);

            var error = Assert.Single(result.Errors);
            Assert.Equal("FirstName", error.PropertyName);
            Assert.Equal("FirstName must not exceed 50 characters", error.ErrorMessage);
        }

        // --- LastName ---

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Validate_EmptyLastName_ReturnsRequiredError(string? lastName)
        {
            var model = ValidModel();
            model.LastName = lastName!;

            var result = _validator.Validate(model);

            var error = Assert.Single(result.Errors);
            Assert.Equal("LastName", error.PropertyName);
            Assert.Equal("LastName is required", error.ErrorMessage);
        }

        [Fact]
        public void Validate_LastNameLongerThan50_ReturnsMaxLengthError()
        {
            var model = ValidModel();
            model.LastName = Repeat('a', NameMaxLength + 1);

            var result = _validator.Validate(model);

            var error = Assert.Single(result.Errors);
            Assert.Equal("LastName", error.PropertyName);
            Assert.Equal("LastName must not exceed 50 characters", error.ErrorMessage);
        }

        // --- CF ---

        [Fact]
        public void Validate_NullCf_ReturnsRequiredError()
        {
            var model = ValidModel();
            model.CF = null!;

            var result = _validator.Validate(model);

            // On null, Length(16) and Matches pass by definition: the only rule
            // that can fail is NotEmpty, hence a single error.
            var error = Assert.Single(result.Errors);
            Assert.Equal("CF", error.PropertyName);
            Assert.Equal("CF is required", error.ErrorMessage);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Validate_BlankCf_ReturnsRequiredLengthAndFormatErrors(string cf)
        {
            var model = ValidModel();
            model.CF = cf;

            var result = _validator.Validate(model);

            // Unlike null, here the string exists: NotEmpty considers it
            // empty, Length(16) fails and the regex does not match it. Three distinct
            // errors on the same property, in declaration order.
            Assert.Equal(
                new[] { "CF is required", "CF must be 16 characters", "CF not valid" },
                Messages(Errors(result, "CF")));
        }

        [Theory]
        [InlineData(15)]
        [InlineData(17)]
        public void Validate_CfWrongLength_ReturnsLengthAndFormatErrors(int length)
        {
            var model = ValidModel();
            model.CF = length < CfLength
                ? ValidCf[..length]
                : ValidCf + Repeat('Z', length - CfLength);

            var result = _validator.Validate(model);

            // The format is otherwise correct: it is the length that fails both
            // Length(16) and the regex, which requires exactly 16 characters.
            Assert.Equal(
                new[] { "CF must be 16 characters", "CF not valid" },
                Messages(Errors(result, "CF")));
        }

        [Theory]
        [InlineData("rssmra80a01h501u")]
        [InlineData("1SSMRA80A01H501U")]
        [InlineData("RSS1RA80A01H501U")]
        [InlineData("RSSMRA80A01H50UU")]
        public void Validate_CfWrongFormat_ReturnsOnlyFormatError(string cf)
        {
            var model = ValidModel();
            model.CF = cf;

            var result = _validator.Validate(model);

            // All cases are 16 characters long, so Length passes and only
            // the regex remains: the pattern is positional and case-sensitive, and moving
            // a digit into a letter's place (or vice versa) is enough to invalidate it.
            var error = Assert.Single(Errors(result, "CF"));
            Assert.Equal("CF not valid", error.ErrorMessage);
        }

        // --- Password ---

        [Fact]
        public void Validate_NullPassword_ReturnsRequiredError()
        {
            var model = ValidModel();
            model.Password = null!;

            var result = _validator.Validate(model);

            // MinimumLength(8) skips null, so the duplicate seen below does not
            // appear here: a single error. The confirmation produces a mismatch instead, which is
            // covered by the PasswordConfirmation tests.
            var error = Assert.Single(Errors(result, "Password"));
            Assert.Equal("The password must be at least 8 characters", error.ErrorMessage);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Validate_BlankPassword_ReturnsTwoIdenticalMinimumLengthErrors(string password)
        {
            var model = ValidModel();
            model.Password = password;

            var result = _validator.Validate(model);

            // NotEmpty and MinimumLength(8) declare the same text, so an empty or
            // whitespace-only password produces two identical errors: this is the
            // current behavior of the validator, pinned here as characterization.
            Assert.Equal(
                new[] { "The password must be at least 8 characters", "The password must be at least 8 characters" },
                Messages(Errors(result, "Password")));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(7)]
        public void Validate_PasswordShorterThan8_ReturnsMinimumLengthError(int length)
        {
            var model = ValidModel();
            model.Password = Repeat('a', length);
            model.PasswordConfirmation = model.Password;

            var result = _validator.Validate(model);

            // Not empty, so NotEmpty passes: only MinimumLength(8) fails.
            var error = Assert.Single(Errors(result, "Password"));
            Assert.Equal("The password must be at least 8 characters", error.ErrorMessage);
        }

        [Fact]
        public void Validate_PasswordLongerThan72_ReturnsMaxLengthError()
        {
            var model = ValidModel();
            model.Password = Repeat('a', PasswordMaxLength + 1);
            model.PasswordConfirmation = model.Password;

            var result = _validator.Validate(model);

            // 72 is the BCrypt ceiling: beyond it the hash would be silently truncated,
            // so the validator blocks it earlier, at validation time.
            var error = Assert.Single(Errors(result, "Password"));
            Assert.Equal("The password must not exceed 72 characters", error.ErrorMessage);
        }

        [Theory]
        [InlineData(PasswordMinLength)]
        [InlineData(PasswordMaxLength)]
        public void Validate_PasswordBoundaryLengths_AreAccepted(int length)
        {
            var model = ValidModel();
            model.Password = Repeat('a', length);
            model.PasswordConfirmation = model.Password;

            var result = _validator.Validate(model);

            // Both limits are inclusive: MinimumLength and MaximumLength fail
            // only when the length goes beyond the limit, not when it reaches it.
            Assert.Empty(result.Errors);
        }

        // --- PasswordConfirmation ---

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Validate_EmptyPasswordConfirmation_ReturnsRequiredAndMismatchErrors(string? confirmation)
        {
            var model = ValidModel();
            model.PasswordConfirmation = confirmation!;

            var result = _validator.Validate(model);

            // NotEmpty and Equal both fail: a missing confirmation cannot
            // match a non-empty password, so the two errors add up.
            Assert.Equal(
                new[] { "The password confirmation is required", "The passwords do not match" },
                Messages(Errors(result, "PasswordConfirmation")));
        }

        [Theory]
        [InlineData("Password123")]
        [InlineData("password123!")]
        [InlineData("Password123! ")]
        public void Validate_MismatchedPasswordConfirmation_ReturnsMismatchError(string confirmation)
        {
            var model = ValidModel();
            model.PasswordConfirmation = confirmation;

            var result = _validator.Validate(model);

            // Equal on strings in FluentValidation is case-sensitive and does not normalize
            // whitespace: a different upper-case letter or a trailing space is enough to invalidate.
            var error = Assert.Single(Errors(result, "PasswordConfirmation"));
            Assert.Equal("The passwords do not match", error.ErrorMessage);
        }

        // --- BirthDate ---

        [Fact]
        public void Validate_NullBirthDate_IsAccepted()
        {
            var model = ValidModel();
            model.BirthDate = null;

            var result = _validator.Validate(model);

            // The rule is Must(d => !d.HasValue || ...): the date is optional and
            // its absence is not an error.
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_PastBirthDate_IsAccepted()
        {
            var model = ValidModel();
            model.BirthDate = new DateOnly(1980, 1, 1);

            var result = _validator.Validate(model);

            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_TodayBirthDate_IsAccepted()
        {
            // The date is read right before validating, so the window in which the
            // test could cross midnight is minimal.
            var today = DateOnly.FromDateTime(DateTime.Now);
            var model = ValidModel();
            model.BirthDate = today;

            var result = _validator.Validate(model);

            // The comparison is <=, so today is allowed: this is the boundary
            // that tells <= apart from <. If the comparison were <, this test would fail.
            Assert.Empty(result.Errors);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(365)]
        public void Validate_FutureBirthDate_ReturnsError(int daysAhead)
        {
            var model = ValidModel();
            model.BirthDate = DateOnly.FromDateTime(DateTime.Now).AddDays(daysAhead);

            var result = _validator.Validate(model);

            var error = Assert.Single(result.Errors);
            Assert.Equal("BirthDate", error.PropertyName);
            Assert.Equal("BirthDate cannot be in the future", error.ErrorMessage);
        }

        // --- Cascade ---

        [Fact]
        public void Validate_AllFieldsInvalid_ReturnsErrorsForEveryProperty()
        {
            var model = new RegisterModel
            {
                Username = string.Empty,
                FirstName = string.Empty,
                LastName = string.Empty,
                CF = "X",
                Password = "short",
                PasswordConfirmation = "different",
                BirthDate = DateOnly.FromDateTime(DateTime.Now).AddYears(1)
            };

            var result = _validator.Validate(model);

            Assert.False(result.IsValid);

            // Cascade Continue: no rule short-circuits the others, so every
            // property of the model is reached. The sequence follows the declaration order
            // of the RuleFor calls; on the CF the two errors (length and format)
            // come together, because the only rule that passes is NotEmpty.
            Assert.Equal(
                new[]
                {
                    "Username",
                    "FirstName",
                    "LastName",
                    "CF",
                    "CF",
                    "Password",
                    "PasswordConfirmation",
                    "BirthDate"
                },
                result.Errors.Select(e => e.PropertyName));
        }
    }
}
