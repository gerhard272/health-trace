using System.Linq.Expressions;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Results;
using HealthTrace.BLL.Security;
using HealthTrace.BLL.Services;
using HealthTrace.DAL.Entities;
using HealthTrace.DAL.Repositories.Interfaces;
using Moq;



namespace HealthTrace.Test.BLL.Services
{
    /// <summary>
    /// Class di test per la classe UserService, focalizzata sul metodo RegisterAsync.
    /// Collegata al file UserService.cs nella cartella BLL/Services.
    /// </summary>
    public class UserServiceRegisterTests
    {
        private const string PlainPassword = "Password123!";
        private const string HashedPassword = "$2a$11$abcdefghijklmnopqrstuvwxyz0123456789";

        private static readonly IReadOnlyList<User> NoUsers = Array.Empty<User>();

        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IGenericRepository<User>> _repository = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IPasswordHasher> _passwordHasher = new();
        private readonly Mock<IValidator<RegisterModel>> _validator = new();

        private int _findCallCount;

        private static RegisterModel ValidModel() => new()
        {
            Username = "mario.rossi",
            FirstName = "Mario",
            LastName = "Rossi",
            CF = "RSSMRA80A01H501U",
            Password = PlainPassword,
            PasswordConfirmation = PlainPassword,
            BirthDate = new DateOnly(1980, 1, 1)
        };

        private UserService CreateService()
        {
            _unitOfWork.Setup(u => u.Repository<User>()).Returns(_repository.Object);
            return new UserService(
                _unitOfWork.Object, _mapper.Object, _passwordHasher.Object, _validator.Object);
        }

        private void SetupValidator(bool isValid, params string[] errors)
        {
            var validation = isValid
                ? new ValidationResult()
                : new ValidationResult(errors.Select(e => new ValidationFailure(string.Empty, e)));

            _validator
                .Setup(v => v.ValidateAsync(It.IsAny<RegisterModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(validation);
        }

        private void SetupFindAsync(params IReadOnlyList<User>[] results)
        {
            _findCallCount = 0;
            _repository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    var index = Math.Min(_findCallCount, results.Length - 1);
                    _findCallCount++;
                    return Task.FromResult(results[index]);
                });
        }

        private void SetupMapper(User entity, UserModel model)
        {
            _mapper.Setup(m => m.Map<User>(It.IsAny<RegisterModel>())).Returns(entity);
            _mapper.Setup(m => m.Map<UserModel>(It.IsAny<User>())).Returns(model);
        }

        [Fact]
        public async Task RegisterAsync_InvalidModel_ReturnsValidationError()
        {
            SetupValidator(false, "Username is required", "CF not valid");
            var service = CreateService();

            var result = await service.RegisterAsync(ValidModel());

            Assert.False(result.Success);
            Assert.Equal(ServiceResultType.ValidationError, result.Type);
            Assert.Equal(new[] { "Username is required", "CF not valid" }, result.Errors);

            _repository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
            _passwordHasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
            _repository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateUsername_ReturnsValidationError()
        {
            SetupValidator(true);
            SetupFindAsync(new[] { new User { Username = "mario.rossi" } });
            var service = CreateService();

            var result = await service.RegisterAsync(ValidModel());

            Assert.False(result.Success);
            Assert.Equal(ServiceResultType.ValidationError, result.Type);
            Assert.Equal("Username already in use", Assert.Single(result.Errors));
            Assert.Equal(1, _findCallCount);

            _passwordHasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
            _repository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_DuplicateFiscalCode_ReturnsValidationError()
        {
            var model = ValidModel();
            SetupValidator(true);
            SetupFindAsync(NoUsers, new[] { new User { CF = model.CF } });
            var service = CreateService();

            var result = await service.RegisterAsync(model);

            Assert.False(result.Success);
            Assert.Equal(ServiceResultType.ValidationError, result.Type);
            Assert.Equal("Fiscal code already registered", Assert.Single(result.Errors));
            Assert.Equal(2, _findCallCount);

            _passwordHasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
            _repository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_ValidModel_ReturnsOkWithMappedUser()
        {
            SetupValidator(true);
            SetupFindAsync(NoUsers, NoUsers);
            SetupMapper(new User { Id = 42 }, new UserModel { Id = 42, Username = "mario.rossi" });
            _passwordHasher.Setup(h => h.HashPassword(PlainPassword)).Returns(HashedPassword);
            var service = CreateService();

            var result = await service.RegisterAsync(ValidModel());

            Assert.True(result.Success);
            Assert.Equal(ServiceResultType.Success, result.Type);
            Assert.NotNull(result.Data);
            Assert.Equal(42, result.Data.Id);
            Assert.Equal(2, _findCallCount);

            _passwordHasher.Verify(h => h.HashPassword(PlainPassword), Times.Once);
            _repository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ValidModel_PersistsHashedPasswordNonPlain()
        {
            var model = ValidModel();
            SetupValidator(true);
            SetupFindAsync(NoUsers, NoUsers);
            SetupMapper(new User {Username = model.Username, CF = model.CF}, new UserModel { Id = 1 });
            _passwordHasher.Setup(h => h.HashPassword(PlainPassword)).Returns(HashedPassword);

            User? persisted = null;
            _repository
                .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, _) => persisted = u)
                .Returns(Task.CompletedTask);

            var service = CreateService();
            var result = await service.RegisterAsync(model);

            Assert.True(result.Success);
            Assert.NotNull(persisted);
            Assert.Equal(HashedPassword, persisted.PasswordHash);
            Assert.NotEqual(model.Password, persisted.PasswordHash);
            Assert.Equal(model.Username, persisted.Username);
            Assert.Equal(model.CF, persisted.CF);

            _passwordHasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_ValidModel_PropagatesCancellationToken()
        {
            SetupValidator(true);

            CancellationToken? findToken = null, addToken = null, saveToken = null;

            _unitOfWork.Setup(u => u.Repository<User>()).Returns(_repository.Object);
            _repository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<User, bool>>, CancellationToken>((_, ct) => findToken = ct)
                .ReturnsAsync(NoUsers);
            _repository
                .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((_, ct) => addToken = ct)
                .Returns(Task.CompletedTask);
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(ct => saveToken = ct)
                .ReturnsAsync(1);
            SetupMapper(new User { Id = 7 }, new UserModel { Id = 7 });
            _passwordHasher.Setup(h => h.HashPassword(PlainPassword)).Returns(HashedPassword);

            var service = CreateService();
            using var cts = new CancellationTokenSource();

            var result = await service.RegisterAsync(ValidModel(), cts.Token);

            Assert.True(result.Success);
            Assert.Equal(cts.Token, findToken);
            Assert.Equal(cts.Token, addToken);
            Assert.Equal(cts.Token, saveToken);
        }
    }
}