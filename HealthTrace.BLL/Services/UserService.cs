using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Security;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.BLL.Exceptions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using AppValidationException = HealthTrace.BLL.Exceptions.ValidationException;

namespace HealthTrace.BLL.Services
{
    /// <summary>
    /// Eredita da GenericService&lt;User, UserModel&gt; il CRUD generico e implementa IUserService.
    /// La creazione di un utente avviene SOLO tramite RegisterAsync (il CRUD ereditato non trasporta l'hash).
    /// Gli esiti negativi viaggiano come eccezioni: il mapping verso lo status code HTTP e il
    /// corpo ProblemDetails è compito del mapper, che i servizi non devono conoscere.
    /// </summary>
    public class UserService : GenericService<User, UserModel>, IUserService
    {
        private const string GeneralErrorKey = "general";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IValidator<RegisterModel> _validator;

        public UserService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IPasswordHasher passwordHasher,
            IValidator<RegisterModel> validator)
            : base(unitOfWork, mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _passwordHasher = passwordHasher;
            _validator = validator;
        }

        /// <summary>
        /// Validazione via FluentValidation, controlli di unicità (username, CF),
        /// hashing della password e persistenza via UnitOfWork.
        /// </summary>
        /// <exception cref="AppValidationException">
        /// Modello non valido, oppure username o codice fiscale già registrati. Gli errori
        /// di FluentValidation sono raggruppati per PropertyName, così il corpo della risposta
        /// li espone per campo; le regole senza PropertyName finiscono sotto "general".
        /// </exception>
        public async Task<UserModel> RegisterAsync(
            RegisterModel model,
            CancellationToken cancellationToken = default)
        {
            var validation = await _validator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
                throw new AppValidationException(ToErrorDictionary(validation.Errors));

            var repository = _unitOfWork.Repository<User>();

            if (await repository.FindAsync(u => u.Username == model.Username, cancellationToken) is { Count: > 0 })
                throw new AppValidationException(GeneralErrorKey, "Username already in use");

            if (await repository.FindAsync(u => u.CF == model.CF, cancellationToken) is { Count: > 0 })
                throw new AppValidationException(GeneralErrorKey, "Fiscal code already registered");

            var user = _mapper.Map<User>(model);
            user.PasswordHash = _passwordHasher.HashPassword(model.Password);

            await repository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<UserModel>(user);
        }

        /// <summary>
        /// Verifica le credenziali e restituisce il profilo utente se valide.
        /// FirstOrDefault è sicuro perché l'unicità dello username è garantita in fase di registrazione.
        /// </summary>
        /// <exception cref="AppValidationException">Username o password mancanti.</exception>
        /// <exception cref="UnauthorizedException">Credenziali non valide.</exception>
        public async Task<UserModel> LoginAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new AppValidationException(GeneralErrorKey, "Username and password are required");

            var repository = _unitOfWork.Repository<User>();
            var user = (await repository.FindAsync(u => u.Username == username, cancellationToken))
                .FirstOrDefault();

            if (user is null || !_passwordHasher.VerifyPassword(user.PasswordHash, password))
                throw new UnauthorizedException("Invalid username or password");

            return _mapper.Map<UserModel>(user);
        }

        /// <summary>
        /// Questo overload consente di passare direttamente un LoginModel
        /// invece di username e password separati.
        /// </summary>
        public async Task<UserModel> LoginAsync(
            LoginModel model,
            CancellationToken cancellationToken = default)
        {
            if (model is null)
                throw new AppValidationException(GeneralErrorKey, "Username and password are required");

            return await LoginAsync(model.Username, model.Password, cancellationToken);
        }

        /// <summary>
        /// Raggruppa gli errori di FluentValidation per campo, con la chiave "general"
        /// per le regole che non hanno una PropertyName (ad esempio quelle a livello di oggetto).
        /// </summary>
        private static Dictionary<string, string[]> ToErrorDictionary(
            IEnumerable<ValidationFailure> failures)
        {
            return failures
                .GroupBy(f => string.IsNullOrWhiteSpace(f.PropertyName) ? GeneralErrorKey : f.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(f => f.ErrorMessage).ToArray());
        }
    }
}