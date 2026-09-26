using System.Linq.Expressions;
using AutoMapper;
using FluentValidation;
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
    /// Class di test per la classe UserService, focalizzata sul metodo LoginAsync
    /// (entrambi gli overload: username/password e LoginModel).
    /// </summary>
    public class UserServiceLoginTests
    {
        private const string Username = "mario.rossi";
        private const string PlainPassword = "Password123!";
        private const string HashedPassword = "$2a$11$abcdefghijklmnopqrstuvwxyz0123456789";

        private static readonly IReadOnlyList<User> NoUsers = Array.Empty<User>();

        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IGenericRepository<User>> _repository = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IPasswordHasher> _passwordHasher = new();
        private readonly Mock<IValidator<RegisterModel>> _validator = new();

        private UserService CreateService()
        {
            _unitOfWork.Setup(u => u.Repository<User>()).Returns(_repository.Object);
            return new UserService(
                _unitOfWork.Object, _mapper.Object, _passwordHasher.Object, _validator.Object);
        }

        private void SetupFindAsync(IReadOnlyList<User> result)
        {
            _repository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        // --- Overload username/password ---

        [Theory]
        [InlineData(null, PlainPassword)]
        [InlineData("", PlainPassword)]
        [InlineData(" ", PlainPassword)]
        [InlineData(Username, null)]
        [InlineData(Username, "")]
        [InlineData(Username, " ")]
        public async Task LoginAsync_MissingUsernameOrPassword_ReturnsValidationError(
            string? username, string? password)
        {
            var service = CreateService();

            var result = await service.LoginAsync(username!, password!);

            Assert.False(result.Success);
            Assert.Equal(ServiceResultType.ValidationError, result.Type);
            Assert.Equal("Username and password are required", Assert.Single(result.Errors));

            _repository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
            _passwordHasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_UserNotFound_ReturnsUnauthorized()
        {
            SetupFindAsync(NoUsers);
            var service = CreateService();

            var result = await service.LoginAsync(Username, PlainPassword);

            Assert.False(result.Success);
            Assert.Equal(ServiceResultType.Unauthorized, result.Type);
            Assert.Equal("Invalid username or password", Assert.Single(result.Errors));

            // Nessun utente trovato: VerifyPassword non deve nemmeno essere chiamato.
            _passwordHasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ReturnsUnauthorized()
        {
            var user = new User { Username = Username, PasswordHash = HashedPassword };
            SetupFindAsync(new[] { user });
            _passwordHasher.Setup(h => h.VerifyPassword(HashedPassword, PlainPassword)).Returns(false);
            var service = CreateService();

            var result = await service.LoginAsync(Username, PlainPassword);

            Assert.False(result.Success);
            Assert.Equal(ServiceResultType.Unauthorized, result.Type);
            // Stesso messaggio del caso "utente non trovato": non deve rivelare quale dei due è sbagliato.
            Assert.Equal("Invalid username or password", Assert.Single(result.Errors));

            _passwordHasher.Verify(h => h.VerifyPassword(HashedPassword, PlainPassword), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsOkWithMappedUser()
        {
            var user = new User { Id = 42, Username = Username, PasswordHash = HashedPassword };
            SetupFindAsync(new[] { user });
            _passwordHasher.Setup(h => h.VerifyPassword(HashedPassword, PlainPassword)).Returns(true);
            _mapper.Setup(m => m.Map<UserModel>(user)).Returns(new UserModel { Id = 42, Username = Username });
            var service = CreateService();

            var result = await service.LoginAsync(Username, PlainPassword);

            Assert.True(result.Success);
            Assert.Equal(ServiceResultType.Success, result.Type);
            Assert.NotNull(result.Data);
            Assert.Equal(42, result.Data.Id);
            Assert.Equal(Username, result.Data.Username);

            _passwordHasher.Verify(h => h.VerifyPassword(HashedPassword, PlainPassword), Times.Once);
            // Il login non deve mai ri-hashare la password.
            _passwordHasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_PropagatesCancellationToken()
        {
            var user = new User { Id = 1, Username = Username, PasswordHash = HashedPassword };
            CancellationToken? findToken = null;

            _unitOfWork.Setup(u => u.Repository<User>()).Returns(_repository.Object);
            _repository
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<User, bool>>, CancellationToken>((_, ct) => findToken = ct)
                .ReturnsAsync(new[] { user });
            _passwordHasher.Setup(h => h.VerifyPassword(HashedPassword, PlainPassword)).Returns(true);
            _mapper.Setup(m => m.Map<UserModel>(user)).Returns(new UserModel { Id = 1 });

            var service = CreateService();
            using var cts = new CancellationTokenSource();

            var result = await service.LoginAsync(Username, PlainPassword, cts.Token);

            Assert.True(result.Success);
            Assert.Equal(cts.Token, findToken);

            // Il login è in sola lettura: non deve mai scrivere sul repository.
            _repository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // --- Overload LoginModel ---

        [Fact]
        public async Task LoginAsync_NullModel_ReturnsValidationError()
        {
            var service = CreateService();

            var result = await service.LoginAsync((LoginModel)null!);

            Assert.False(result.Success);
            Assert.Equal(ServiceResultType.ValidationError, result.Type);
            Assert.Equal("Username and password are required", Assert.Single(result.Errors));

            _repository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData(" ", PlainPassword)]
        [InlineData(Username, " ")]
        public async Task LoginAsync_WithModelMissingCredentials_ReturnsValidationError(
            string username, string password)
        {
            var service = CreateService();

            var result = await service.LoginAsync(new LoginModel { Username = username, Password = password });

            Assert.False(result.Success);
            Assert.Equal(ServiceResultType.ValidationError, result.Type);
            Assert.Equal("Username and password are required", Assert.Single(result.Errors));

            _repository.Verify(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
            _passwordHasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_WithModel_DelegatesToUsernamePasswordOverload()
        {
            var user = new User { Id = 42, Username = Username, PasswordHash = HashedPassword };
            SetupFindAsync(new[] { user });
            _passwordHasher.Setup(h => h.VerifyPassword(HashedPassword, PlainPassword)).Returns(true);
            _mapper.Setup(m => m.Map<UserModel>(user)).Returns(new UserModel { Id = 42, Username = Username });
            var service = CreateService();

            var model = new LoginModel { Username = Username, Password = PlainPassword };
            var result = await service.LoginAsync(model);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(42, result.Data.Id);
        }
    }
}