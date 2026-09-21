using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Results;
using HealthTrace.BLL.Security;
using HealthTrace.BLL.Services.Interfaces;
using AutoMapper;

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

        public UserService(IUnitOfWork unitOfWork, IMapper mapper, IPasswordHasher passwordHasher)
            : base(unitOfWork, mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Validazione minima, controlli di unicità (username, CF),
        /// hashing della password e persistenza via UnitOfWork.
        /// </summary>
        public async Task<ServiceResult<UserModel>> RegisterAsync(RegisterModel model, CancellationToken cancellationToken = default)
        {
            var errors = Validate(model);
            if (errors.Count > 0)
                return ServiceResult<UserModel>.ValidationError(errors);

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

        // Validazione minima: il task "GESTIONE ERRORI SU REGISTRAZIONE E VALIDAZIONE INPUT" la estenderà.
        private static List<string> Validate(RegisterModel model)
        {
            throw new NotImplementedException();
        }
    }
}