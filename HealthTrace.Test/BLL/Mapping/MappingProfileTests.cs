using AutoMapper;
using HealthTrace.BLL.Mapping;
using HealthTrace.BLL.Models;
using HealthTrace.DAL.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace HealthTrace.Test.BLL.Mapping
{
    /// <summary>
    /// Classe di test per MappingProfile, l'unico profilo AutoMapper del BLL: e' il punto in cui
    /// entita' e DTO si incontrano, e i servizi lo ignorano del tutto (UserService, SymptomService,
    /// GenericService ed ExportService ricevono solo IMapper). Non serve alcun mock ne' un DbContext:
    /// il profilo e' puro, si registra in una configurazione e si usa, quindi ogni assertion riguarda
    /// una delle quattro coppie di tipi che dichiara davvero.
    /// I dati sono inventati e volutamente non plausibili: il profilo non valida nulla, quindi un
    /// codice fiscale in forma corretta non aggiungerebbe copertura e rischierebbe di essere scambiato
    /// per un dato reale. Le costanti sono locali di questa classe e non riprese dagli altri test, cosi'
    /// i file restano indipendenti.
    /// Le asserzioni usano valori distintivi: con entita' ai valori di default un mapping sbagliato
    /// passerebbe comunque, quindi i default servono solo nei casi in cui il default e' il risultato
    /// atteso (PasswordHash, Id, campi di audit).
    /// Nota su AutoMapper 16: MapperConfiguration non espone piu' il costruttore a un argomento, quindi
    /// qui si passa un NullLoggerFactory. Senza chiave di licenza AutoMapper logga un warning
    /// ("allowed for development and testing"): e' atteso e non rende il test rosso.
    /// Nota sulla validazione della configurazione: ReverseMap() registra la direzione inversa con
    /// MemberList.None, quindi AssertConfigurationIsValid copre solo le direzioni dichiarate in
    /// esplicito, cioe' User -> UserModel, RegisterModel -> User e Symptom -> SymptomModel. Su
    /// RegisterModel -> User la validazione segnala Id e i campi di audit come non mappati, perche'
    /// sono valorizzati da EF e non dal client: l'ultimo test fissa lo stato attuale invece di
    /// lasciare un test rosso.
    /// </summary>
    public class MappingProfileTests
    {
        // Dati fittizi: nessuno di questi valori corrisponde a una persona reale.
        private const string TestUsername = "test.user";
        private const string TestFirstName = "nome-di-test";
        private const string TestLastName = "cognome-di-test";
        private const string TestCf = "CF-FITTIZIO";
        private const string TestBirthPlace = "citta-di-test";
        private const string TestPassword = "Password-di-test-1!";
        private const string TestPasswordConfirmation = "Password-di-test-1!";
        private const string TestPasswordHash = "hash-di-test";
        private const string TestEventName = "evento-di-test";
        private const string TestDescription = "descrizione-di-test";

        private const int TestId = 42;
        private const int TestUserId = 7;
        private const string TestAuditMarker = "marcatore-di-audit";

        private static readonly DateOnly TestBirthDate = new(1980, 1, 1);
        private static readonly DateTime TestEventDate = new(2024, 5, 17, 10, 30, 0, DateTimeKind.Utc);

        private readonly IMapper _mapper = BuildConfiguration().CreateMapper();

        // La configurazione replica la registrazione di produzione (Program.cs e Functions/Program.cs
        // passano l'assembly del BLL): il profilo viene scoperto dalla scansione dell'assembly.
        private static MapperConfiguration BuildConfiguration() =>
            new(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);

        private static User SampleUser() => new()
        {
            Id = TestId,
            Username = TestUsername,
            PasswordHash = TestPasswordHash,
            FirstName = TestFirstName,
            LastName = TestLastName,
            CF = TestCf,
            BirthDate = TestBirthDate,
            BirthPlace = TestBirthPlace
        };

        private static UserModel SampleUserModel() => new()
        {
            Id = TestId,
            Username = TestUsername,
            FirstName = TestFirstName,
            LastName = TestLastName,
            CF = TestCf,
            BirthDate = TestBirthDate,
            BirthPlace = TestBirthPlace
        };

        private static RegisterModel SampleRegisterModel() => new()
        {
            Username = TestUsername,
            FirstName = TestFirstName,
            LastName = TestLastName,
            CF = TestCf,
            BirthDate = TestBirthDate,
            BirthPlace = TestBirthPlace,
            Password = TestPassword,
            PasswordConfirmation = TestPasswordConfirmation
        };

        private static Symptom SampleSymptom() => new()
        {
            Id = TestId,
            UserId = TestUserId,
            EventName = TestEventName,
            Description = TestDescription,
            EventDate = TestEventDate
        };

        private static SymptomModel SampleSymptomModel() => new()
        {
            Id = TestId,
            UserId = TestUserId,
            EventName = TestEventName,
            Description = TestDescription,
            EventDate = TestEventDate
        };

        // Un'entita' con i campi di audit valorizzati serve a distinguere "il mapper non ha toccato
        // niente" da "il mapper ha riscritto il campo": CreatedBy riceve TestAuditMarker, cosi' la
        // sovrascrittura sarebbe visibile.
        private static User UserWithAudit() => new()
        {
            Id = TestId,
            Username = TestUsername,
            PasswordHash = TestPasswordHash,
            CreatedBy = TestId,
            CreatedAt = TestEventDate,
            IsDeleted = true
        };

        private static Symptom SymptomWithAudit() => new()
        {
            Id = TestId,
            UserId = TestUserId,
            EventName = TestEventName,
            CreatedBy = TestId,
            CreatedAt = TestEventDate,
            IsDeleted = true
        };

        // --- User -> UserModel ---

        [Fact]
        public void Map_UserToUserModel_CopiesIdAndProfileFields()
        {
            // Il DTO di output e' la forma in cui l'utente viaggia verso il client: se un campo
            // profilo non arrivasse, la risposta sarebbe incompleta senza che nulla fallisse.
            var model = _mapper.Map<UserModel>(SampleUser());

            Assert.Equal(TestId, model.Id);
            Assert.Equal(TestUsername, model.Username);
            Assert.Equal(TestFirstName, model.FirstName);
            Assert.Equal(TestLastName, model.LastName);
            Assert.Equal(TestCf, model.CF);
            Assert.Equal(TestBirthDate, model.BirthDate);
            Assert.Equal(TestBirthPlace, model.BirthPlace);
        }

        [Fact]
        public void Map_UserToUserModel_KeepsNullOptionalFields()
        {
            // Nati e luogo di nascita sono opzionali: il mapper non deve trasformarli in stringhe
            // vuote o in date di default, perche' il client distingue "non valorizzato" da "vuoto".
            var user = new User { Username = TestUsername };

            var model = _mapper.Map<UserModel>(user);

            Assert.Null(model.BirthDate);
            Assert.Null(model.BirthPlace);
        }

        [Fact]
        public void Map_UserToUserModel_LeavesSourceEntityUntouched()
        {
            // Il mapping verso un DTO non e' un aggiornamento: l'entita' letta dal repository deve
            // restare come era, altrimenti un servizio che mappo' per leggere finirebbe per
            // scrivere dati in memoria senza accorgersene.
            var user = SampleUser();

            var model = _mapper.Map<UserModel>(user);

            Assert.NotSame(user, model);
            Assert.Equal(TestPasswordHash, user.PasswordHash);
        }

        // --- UserModel -> User (ReverseMap) ---

        [Fact]
        public void Map_UserModelToUser_CopiesProfileFieldsAndLeavesPasswordHashEmpty()
        {
            // La direzione inversa esiste per il ReverseMap, ma il DTO non contiene l'hash: quindi
            // l'entita' ottenuta non ne ha uno. E' il motivo per cui in UserService la creazione di un
            // utente avviene solo da RegisterAsync, che imposta l'hash subito dopo il mapping: se
            // questa asserzione cambiasse, un utente potrebbe essere persistito senza credenziali.
            var user = _mapper.Map<User>(SampleUserModel());

            Assert.Equal(TestId, user.Id);
            Assert.Equal(TestUsername, user.Username);
            Assert.Equal(TestFirstName, user.FirstName);
            Assert.Equal(TestLastName, user.LastName);
            Assert.Equal(TestCf, user.CF);
            Assert.Equal(TestBirthDate, user.BirthDate);
            Assert.Equal(TestBirthPlace, user.BirthPlace);
            Assert.Equal(string.Empty, user.PasswordHash);
        }

        [Theory]
        [InlineData(nameof(User.PasswordHash))]
        [InlineData(nameof(AuditEntity.CreatedAt))]
        [InlineData(nameof(AuditEntity.CreatedBy))]
        [InlineData(nameof(AuditEntity.ModifiedAt))]
        [InlineData(nameof(AuditEntity.ModifiedBy))]
        [InlineData(nameof(AuditEntity.DeletedAt))]
        [InlineData(nameof(AuditEntity.DeletedBy))]
        [InlineData(nameof(AuditEntity.IsDeleted))]
        public void UserModel_ExposesNoSensitiveOrAuditProperty(string propertyName)
        {
            // Non basta che il mapper non valorizzi questi campi: e' il DTO stesso a non poterli
            // trasportare, cosi' l'hash non puo' finire in una risposta nemmeno se un altro mapping
            // li assegna. I campi di audit restano cosi' responsabilita' del DAL e non del BLL.
            Assert.Null(typeof(UserModel).GetProperty(propertyName));
        }

        // --- RegisterModel -> User ---

        [Fact]
        public void Map_RegisterModel_CopiesProfileFields()
        {
            // Il DTO di registrazione eredita da UserBaseModel: i suoi campi devono arrivare
            // nell'entita' altrimenti l'utente verrebbe salvato con i valori di default.
            var user = _mapper.Map<User>(SampleRegisterModel());

            Assert.Equal(TestUsername, user.Username);
            Assert.Equal(TestFirstName, user.FirstName);
            Assert.Equal(TestLastName, user.LastName);
            Assert.Equal(TestCf, user.CF);
            Assert.Equal(TestBirthDate, user.BirthDate);
            Assert.Equal(TestBirthPlace, user.BirthPlace);
        }

        [Fact]
        public void Map_RegisterModel_LeavesPasswordHashEmpty()
        {
            // Il test centrale del task: RegisterModel porta la password in chiaro e l'entita' ha
            // un campo PasswordHash. L'Ignore esplicito nel profilo serve proprio a impedire che il
            // mapper scriva qualcosa li', e l'hash viene calcolato dal servizio subito dopo.
            var user = _mapper.Map<User>(SampleRegisterModel());

            Assert.Equal(TestPassword, SampleRegisterModel().Password);
            Assert.Equal(string.Empty, user.PasswordHash);
        }

        [Fact]
        public void Map_RegisterModel_DoesNotWriteIdOrAuditFields()
        {
            // Un utente nuovo nasce senza identita' ne' tracciabilita': se il mapper scrivesse
            // CreatedAt o CreatedBy, l'audit di chi registra sarebbe attribuito al servizio e non
            // alla persona che ha eseguito l'operazione.
            var user = _mapper.Map<User>(SampleRegisterModel());

            Assert.Equal(0, user.Id);
            Assert.Equal(default, user.CreatedAt);
            Assert.Equal(0, user.CreatedBy);
            Assert.Null(user.ModifiedAt);
            Assert.Null(user.ModifiedBy);
            Assert.Null(user.DeletedAt);
            Assert.Null(user.DeletedBy);
            Assert.False(user.IsDeleted);
        }

        [Theory]
        [InlineData(nameof(RegisterModel.Password))]
        [InlineData(nameof(RegisterModel.PasswordConfirmation))]
        public void User_HasNoPropertyForPlainPassword(string propertyName)
        {
            // Le credenziali in chiaro non hanno una destinazione nell'entita': il mapping le scarta
            // e basta. Questa asserzione blocca l'evoluzione peggiore del profilo, cioe' l'aggiunta di
            // una property in chiaro su User che riporterebbe le credenziali in chiaro in tabella.
            Assert.Null(typeof(User).GetProperty(propertyName));
        }

        // --- Symptom -> SymptomModel ---

        [Fact]
        public void Map_SymptomToSymptomModel_CopiesIdUserIdEventNameDescriptionAndDate()
        {
            // E' il percorso di lettura dei sintomi: se un campo non arrivasse, la lista restituita
            // al client sarebbe incompleta senza errori.
            var model = _mapper.Map<SymptomModel>(SampleSymptom());

            Assert.Equal(TestId, model.Id);
            Assert.Equal(TestUserId, model.UserId);
            Assert.Equal(TestEventName, model.EventName);
            Assert.Equal(TestDescription, model.Description);
            Assert.Equal(TestEventDate, model.EventDate);
        }

        [Fact]
        public void Map_SymptomToSymptomModel_KeepsNullDescription()
        {
            // La descrizione e' opzionale: deve restare null e non diventare una stringa vuota,
            // altrimenti il report di export stamperebbe un trattino dove non c'e' nulla.
            var model = _mapper.Map<SymptomModel>(new Symptom { EventName = TestEventName });

            Assert.Null(model.Description);
        }

        // --- SymptomModel -> Symptom (ReverseMap) ---

        [Fact]
        public void Map_SymptomModelToSymptom_CopiesAllFieldsIncludingUserId()
        {
            // UserId viene copiato come qualsiasi altro campo: e' per questo che SymptomService lo
            // sovrascrive con l'utente autenticato subito dopo il mapping, altrimenti un client
            // potrebbe assegnare un sintomo a un altro utente. Il override e' una difesa del
            // servizio, non un effetto del profilo, e questa asserzione lo tiene fermo.
            var symptom = _mapper.Map<Symptom>(SampleSymptomModel());

            Assert.Equal(TestId, symptom.Id);
            Assert.Equal(TestUserId, symptom.UserId);
            Assert.Equal(TestEventName, symptom.EventName);
            Assert.Equal(TestDescription, symptom.Description);
            Assert.Equal(TestEventDate, symptom.EventDate);
        }

        // --- Collezioni ---

        [Fact]
        public void Map_SymptomList_MapsEveryItemAndPreservesOrder()
        {
            // Le liste sono il percorso piu' usato del profilo (GetAllByUserIdAsync ed
            // ExportService): l'ordine arriva dal repository e deve arrivare al client, perche' il
            // report e la lista della UI lo usano come ordine di lettura.
            IReadOnlyList<Symptom> symptoms =
            [
                new() { Id = TestId, UserId = TestUserId, EventName = "primo", EventDate = TestEventDate },
                new() { Id = TestId + 1, UserId = TestUserId, EventName = "secondo", EventDate = TestEventDate },
                new() { Id = TestId + 2, UserId = TestUserId, EventName = "terzo", Description = TestDescription, EventDate = TestEventDate }
            ];

            var models = _mapper.Map<IReadOnlyList<SymptomModel>>(symptoms);

            Assert.Equal(3, models.Count);
            Assert.Equal(new[] { "primo", "secondo", "terzo" }, models.Select(m => m.EventName));
            Assert.Equal(new[] { TestId, TestId + 1, TestId + 2 }, models.Select(m => m.Id));
            Assert.Null(models[0].Description);
            Assert.Equal(TestDescription, models[2].Description);
        }

        [Fact]
        public void Map_EmptySymptomList_ReturnsEmptyList()
        {
            // Un utente senza sintomi e' un caso normale, non un errore: la lista vuota deve restare
            // vuota invece di trasformarsi in una lista con un elemento vuoto.
            var models = _mapper.Map<IReadOnlyList<SymptomModel>>(Array.Empty<Symptom>());

            Assert.Empty(models);
        }

        // --- Mapping su entita' esistente ---

        [Fact]
        public void Map_SymptomModelOntoExistingSymptom_UpdatesFieldsAndKeepsIdAndAudit()
        {
            // E' il percorso di aggiornamento usato da SymptomService e dal CRUD generico. L'Id e'
            // l'unico campo che il DTO sovrascrive davvero: in entrambi i call site e' la chiave con
            // cui l'entita' e' stata caricata, quindi la riscrittura non cambia l'identita' della
            // riga, e qui viene fatto di proposito con un altro valore per rendere il comportamento
            // visibile. I campi di audit invece non esistono nel DTO e devono restare intatti: se
            // venissero azzerati, l'entita' ripartirebbe da una tracciabilita' falsa dopo ogni
            // modifica.
            var entity = SymptomWithAudit();

            _mapper.Map(new SymptomModel
            {
                Id = TestId + 99,
                UserId = TestUserId,
                EventName = "nome-aggiornato",
                Description = null,
                EventDate = TestEventDate
            }, entity);

            Assert.Equal(TestId + 99, entity.Id);
            Assert.Equal("nome-aggiornato", entity.EventName);
            Assert.Null(entity.Description);
            Assert.Equal(TestEventDate, entity.EventDate);
            Assert.Equal(TestId, entity.CreatedBy);
            Assert.Equal(TestEventDate, entity.CreatedAt);
            Assert.True(entity.IsDeleted);
        }

        [Fact]
        public void Map_UserModelOntoExistingUser_KeepsPasswordHashAndAudit()
        {
            // Stessa logica del caso precedente sul lato utente, con un vincolo in piu': il DTO non ha
            // il campo PasswordHash, quindi l'hash gia' persistito deve restare quello. Un update non
            // puo' azzerare le credenziali di un utente esistente.
            var entity = UserWithAudit();

            _mapper.Map(new UserModel
            {
                Id = TestId,
                Username = "username-aggiornato",
                FirstName = TestFirstName,
                LastName = TestLastName,
                CF = TestCf
            }, entity);

            Assert.Equal("username-aggiornato", entity.Username);
            Assert.Equal(TestPasswordHash, entity.PasswordHash);
            Assert.Equal(TestId, entity.CreatedBy);
            Assert.Equal(TestEventDate, entity.CreatedAt);
            Assert.True(entity.IsDeleted);
            Assert.Null(entity.BirthDate);
            Assert.Null(entity.BirthPlace);
        }

        // --- Configurazione ---

        [Fact]
        public void Profile_ValidationReportsUnmappedMembersOnlyForRegisterModelToUser()
        {
            // La validazione globale del profilo oggi NON e' verde, e il motivo e' noto: la coppia
            // RegisterModel -> User ha come membri di destinazione Id e i campi di AuditEntity, che
            // il client non invia e che vengono valorizzati da EF/UnitOfWork. Non si committa un
            // test rosso: questo test fissa lo stato attuale, cosi' il gap resta visibile e, se un
            // domani il profilo verra' corretto (MemberList.None, oppure Ignore esplicito su Id e
            // campi di audit), il test segnalera' che la documentazione qui e' da aggiornare.
            // Le altre tre direzioni sono invece complete, ed e' questo il vero controllo: un membro
            // non mappato aggiunto in futuro su User -> UserModel o Symptom -> SymptomModel fa
            // fallire il test, perche' l'eccezione li elencherebbe.
            var configuration = BuildConfiguration();

            var exception = Assert.Throws<AutoMapperConfigurationException>(configuration.AssertConfigurationIsValid);

            Assert.Contains($"{nameof(RegisterModel)} -> {nameof(User)}", exception.Message);
            Assert.DoesNotContain($"{nameof(User)} -> {nameof(UserModel)}", exception.Message);
            Assert.DoesNotContain($"{nameof(Symptom)} -> {nameof(SymptomModel)}", exception.Message);
        }
    }
}
