using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Results;
using HealthTrace.BLL.Security;
using HealthTrace.BLL.Services.Interfaces;
using AutoMapper;
using FluentValidation;

namespace HealthTrace.BLL.Services
{
    /// <summary>
    /// Eredita da GenericService&lt;User, UserModel&gt; il CRUD generico e implementa IUserService.
    /// La creazione di un utente avviene SOLO tramite RegisterAsync (il CRUD ereditato non trasporta l'hash).
    /// </summary>
    public class UserService : GenericService<User, UserModel>, IUserService
    {
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
        public async Task<ServiceResult<UserModel>> RegisterAsync(
            RegisterModel model,
            CancellationToken cancellationToken = default)
        {
            var validation = await _validator.ValidateAsync(model, cancellationToken);
            if (!validation.IsValid)
                return ServiceResult<UserModel>.ValidationError(
                    validation.Errors.Select(e => e.ErrorMessage).ToList());

            var repository = _unitOfWork.Repository<User>();

            if (await repository.FindAsync(u => u.Username == model.Username, cancellationToken) is { Count: > 0 })
                return ServiceResult<UserModel>.ValidationError(["Username already in use"]);

            if (await repository.FindAsync(u => u.CF == model.CF, cancellationToken) is { Count: > 0 })
                return ServiceResult<UserModel>.ValidationError(["Fiscal code already registered"]);

            var user = _mapper.Map<User>(model);
            user.PasswordHash = _passwordHasher.HashPassword(model.Password);

            await repository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ServiceResult<UserModel>.Ok(_mapper.Map<UserModel>(user));
        }

        /// <summary>
        /// Verifica le credenziali e restituisce il profilo utente se valide.
        /// SingleOrDefault è sicuro perché l'unicità dello username è garantita in fase di registrazione.
        /// </summary>
        public async Task<ServiceResult<UserModel>> LoginAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            var repository = _unitOfWork.Repository<User>();
            var user = (await repository.FindAsync(u => u.Username == username, cancellationToken))
                .SingleOrDefault();

            if (user is null || !_passwordHasher.VerifyPassword(user.PasswordHash, password))
                return ServiceResult<UserModel>.Unauthorized("Invalid username or password");

            return ServiceResult<UserModel>.Ok(_mapper.Map<UserModel>(user));
        }
    }
}