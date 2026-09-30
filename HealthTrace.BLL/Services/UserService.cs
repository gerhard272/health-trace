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
    /// Inherits the generic CRUD from GenericService&lt;User, UserModel&gt; and implements IUserService.
    /// Users are created ONLY through RegisterAsync (the inherited CRUD does not carry the hash).
    /// Failures travel as exceptions: mapping them to the HTTP status code and
    /// ProblemDetails body is the mapper's job, which services must not know about.
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
        /// Validation via FluentValidation, uniqueness checks (username, fiscal code),
        /// password hashing and persistence via UnitOfWork.
        /// </summary>
        /// <exception cref="AppValidationException">
        /// Invalid model, or username or fiscal code already registered. FluentValidation
        /// errors are grouped by PropertyName, so the response body exposes them
        /// per field; rules without a PropertyName end up under "general".
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
        /// Checks the credentials and returns the user profile if they are valid.
        /// FirstOrDefault is safe because username uniqueness is enforced at registration.
        /// </summary>
        /// <exception cref="AppValidationException">Missing username or password.</exception>
        /// <exception cref="UnauthorizedException">Invalid credentials.</exception>
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
        /// This overload allows passing a LoginModel directly
        /// instead of separate username and password.
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
        /// Groups FluentValidation errors by field, using the "general" key
        /// for rules without a PropertyName (e.g. object-level rules).
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