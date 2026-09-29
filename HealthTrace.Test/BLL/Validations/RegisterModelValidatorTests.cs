using FluentValidation;
using FluentValidation.Results;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Validations;

namespace HealthTrace.Test.BLL.Validations
{
    /// <summary>
    /// Classe di test per RegisterModelValidator, l'unico validatore FluentValidation
    /// del BLL. Non servono mock: il validator e' puro, si istanzia e si invoca
    /// direttamente, quindi ogni assertion riguarda una regola reale.
    /// Due dettagli del framework guidano le aspettative:
    /// - cascade Continue (default): una regola che fallisce non cortocircuita le altre
    ///   sulla stessa proprieta', quindi un campo puo' generare piu' errori;
    /// - i validatori di lunghezza e Matches sono veri solo su stringhe non nulle, mentre
    ///   NotEmpty considera vuota anche la stringa composta solo da spazi.
    /// I test che fissano piu' di un errore documentano il comportamento reale.
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

        // Isola gli errori di una sola proprieta'. Serve perche' una stessa anomalia
        // (password o conferma assente) fa fallire piu' regole, e i test vogliono
        // asserire sul contratto di un campo senza dipendere dagli altri.
        private static ValidationFailure[] Errors(ValidationResult result, string property) =>
            result.Errors.Where(e => e.PropertyName == property).ToArray();

        private static string[] Messages(IEnumerable<ValidationFailure> errors) =>
            errors.Select(e => e.ErrorMessage).ToArray();

        // --- Percorso felice ---

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

            // MaximumLength(50) non aggiunge errori: sul null lo salta, sulla stringa
            // vuota o di spazi la soglia e' rispettata. Resta solo l'obbligatorietà.
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

            // La soglia e' inclusa: fallirebbe solo un carattere oltre.
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

            // Sul null Length(16) e Matches sono veri per definizione: l'unica regola
            // che puo' fallire e' NotEmpty, quindi un solo errore.
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

            // Diversamente dal null, qui la stringa esiste: NotEmpty la considera
            // vuota, Length(16) fallisce e la regex non la riconosce. Tre errori
            // distinti sulla stessa proprieta', nell'ordine di dichiarazione.
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

            // Il formato e' altrimenti corretto: e' la lunghezza a far fallire sia
            // Length(16) sia la regex, che richiede esattamente 16 caratteri.
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

            // Tutti i casi sono lunghi 16 caratteri, quindi Length passa e resta solo
            // la regex: il pattern e' posizionale e case-sensitive, e basta spostare
            // una cifra al posto di una lettera (o il contrario) per invaliderlo.
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

            // MinimumLength(8) salta il null, quindi qui non nasce il duplicato che si
            // vede sotto: un solo errore. La conferma genera invece un mismatch, ed e'
            // coperto dai test di PasswordConfirmation.
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

            // NotEmpty e MinimumLength(8) dichiarano lo stesso testo, quindi una
            // password vuota o di soli spazi produce due errori identici: e' il
            // comportamento attuale del validator, fissato qui come caratterizzazione.
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

            // Non vuota, quindi NotEmpty passa: a fallire e' solo MinimumLength(8).
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

            // 72 e' il tetto di BCrypt: oltre l'hash verrebbe troncato in silenzio,
            // quindi il validator lo blocca prima, in validazione.
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

            // Le due soglie sono incluse: MinimumLength e MaximumLength falliscono
            // solo se la lunghezza supera il limite, non se lo raggiunge.
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

            // NotEmpty ed Equal falliscono entrambe: la conferma mancante non puo'
            // coincidere con una password valorizzata, quindi i due errori si sommano.
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

            // Equal su stringhe in FluentValidation e' case-sensitive e non normalizza
            // gli spazi: basta un maiuscolo diverso o uno spazio finale per invalidare.
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

            // La regola e' Must(d => !d.HasValue || ...): la data e' opzionale e la
            // sua assenza non e' un errore.
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
            // La data e' letta subito prima di validare, cosi' la finestra in cui il
            // test puo' incrociare la mezzanotte e' minima.
            var today = DateOnly.FromDateTime(DateTime.Now);
            var model = ValidModel();
            model.BirthDate = today;

            var result = _validator.Validate(model);

            // Il confronto e' <=, quindi il giorno corrente e' ammesso: e' il boundary
            // che distingue <= da <. Se il confronto fosse <, questo test fallirebbe.
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

        // --- Cascata ---

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

            // Cascade Continue: nessuna regola cortocircuita le altre, quindi ogni
            // proprieta' del modello viene raggiunta. La sequenza segue l'ordine di
            // dichiarazione delle RuleFor; sul CF i due errori (lunghezza e formato)
            // stanno insieme, perche' l'unica regola superata e' NotEmpty.
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
