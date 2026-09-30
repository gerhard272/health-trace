using System.Reflection;
using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
// Il namespace di questo file replica quello del codice sotto test, quindi la
// regola di risoluzione dei namespace annidati non trova da sola il controller:
// la dichiarazione esplicita serve.
using HealthTrace.PL.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using AppValidationException = HealthTrace.BLL.Exceptions.ValidationException;

namespace HealthTrace.Test.PL.API.Controllers
{
    /// <summary>
    /// Classe di test per AuthController. Il controller non contiene logica propria:
    /// delega a IUserService e traduce l'esito positivo in un ActionResult. I test
    /// verificano quindi tre cose: lo status code e il corpo della risposta di successo,
    /// il passaggio invariato di modello e CancellationToken al servizio, e il fatto
    /// che le eccezioni applicative non vengano intercettate qui, perché la loro
    /// traduzione in ProblemDetails spetta al gestore globale (ExceptionStatusMapper).
    /// </summary>
    public class AuthControllerTests
    {
        private const string Username = "mario.rossi";
        private const string PlainPassword = "Password123!";

        private readonly Mock<IUserService> _userService = new();

        private AuthController CreateController() => new(_userService.Object);

        private static RegisterModel ValidRegisterModel() => new()
        {
            Username = Username,
            FirstName = "Mario",
            LastName = "Rossi",
            CF = "RSSMRA80A01H501U",
            Password = PlainPassword,
            PasswordConfirmation = PlainPassword,
            BirthDate = new DateOnly(1980, 1, 1)
        };

        private static LoginModel ValidLoginModel() => new()
        {
            Username = Username,
            Password = PlainPassword
        };

        private static UserModel RegisteredUser() => new()
        {
            Id = 42,
            Username = Username,
            FirstName = "Mario",
            LastName = "Rossi",
            CF = "RSSMRA80A01H501U"
        };

        // --- Register ---

        [Fact]
        public async Task Register_ValidModel_Returns201CreatedWithUser()
        {
            var user = RegisteredUser();
            _userService
                .Setup(s => s.RegisterAsync(It.IsAny<RegisterModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            var controller = CreateController();

            var result = await controller.Register(ValidRegisterModel(), CancellationToken.None);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
            Assert.Equal(nameof(AuthController.Register), created.ActionName);

            // Il corpo è esattamente il modello restituito dal servizio, senza copie.
            Assert.Same(user, created.Value);
        }

        [Fact]
        public async Task Register_PassesModelAndCancellationTokenToService()
        {
            var model = ValidRegisterModel();
            using var cts = new CancellationTokenSource();
            _userService
                .Setup(s => s.RegisterAsync(model, cts.Token))
                .ReturnsAsync(RegisteredUser());
            var controller = CreateController();

            await controller.Register(model, cts.Token);

            _userService.Verify(s => s.RegisterAsync(model, cts.Token), Times.Once);
            // La registrazione non deve passare da nessun altro metodo del servizio,
            // in particolare non dal Create generico, che non gestisce l'hash.
            _userService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Register_ServiceThrowsValidationException_PropagatesException()
        {
            var exception = new AppValidationException("username", "Username already in use");
            _userService
                .Setup(s => s.RegisterAsync(It.IsAny<RegisterModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
            var controller = CreateController();

            // Il controller non deve catturare l'eccezione né trasformarla in un
            // BadRequest: la traduzione è compito del gestore globale.
            var thrown = await Assert.ThrowsAsync<AppValidationException>(
                () => controller.Register(ValidRegisterModel(), CancellationToken.None));

            Assert.Same(exception, thrown);
        }

        // --- Login ---

        [Fact]
        public async Task Login_ValidCredentials_Returns200OkWithUser()
        {
            var user = RegisteredUser();
            _userService
                .Setup(s => s.LoginAsync(It.IsAny<LoginModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            var controller = CreateController();

            var result = await controller.Login(ValidLoginModel(), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.Same(user, ok.Value);
        }

        [Fact]
        public async Task Login_PassesModelAndCancellationTokenToService()
        {
            var model = ValidLoginModel();
            using var cts = new CancellationTokenSource();
            _userService
                .Setup(s => s.LoginAsync(model, cts.Token))
                .ReturnsAsync(RegisteredUser());
            var controller = CreateController();

            await controller.Login(model, cts.Token);

            // Il controller usa l'overload con LoginModel: quello username/password
            // non deve essere chiamato direttamente.
            _userService.Verify(s => s.LoginAsync(model, cts.Token), Times.Once);
            _userService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Login_ServiceThrowsUnauthorizedException_PropagatesException()
        {
            var exception = new UnauthorizedException("Invalid username or password");
            _userService
                .Setup(s => s.LoginAsync(It.IsAny<LoginModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
            var controller = CreateController();

            var thrown = await Assert.ThrowsAsync<UnauthorizedException>(
                () => controller.Login(ValidLoginModel(), CancellationToken.None));

            Assert.Same(exception, thrown);
        }

        [Fact]
        public async Task Login_ServiceThrowsValidationException_PropagatesException()
        {
            var exception = new AppValidationException("general", "Username and password are required");
            _userService
                .Setup(s => s.LoginAsync(It.IsAny<LoginModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
            var controller = CreateController();

            var thrown = await Assert.ThrowsAsync<AppValidationException>(
                () => controller.Login(new LoginModel(), CancellationToken.None));

            Assert.Same(exception, thrown);
        }

        // --- Attributi di routing e autorizzazione ---

        [Fact]
        public void Controller_IsAnonymousApiControllerWithAuthRoute()
        {
            var type = typeof(AuthController);

            // Register e Login devono essere raggiungibili senza autenticazione:
            // senza AllowAnonymous nessun utente potrebbe ottenere l'accesso.
            Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal("api/[controller]", type.GetCustomAttribute<RouteAttribute>()?.Template);
        }

        [Theory]
        [InlineData(nameof(AuthController.Register), "register")]
        [InlineData(nameof(AuthController.Login), "login")]
        public void Action_IsHttpPostWithExpectedTemplate(string actionName, string expectedTemplate)
        {
            var method = typeof(AuthController).GetMethod(actionName)!;

            var httpPost = method.GetCustomAttribute<HttpPostAttribute>();

            // POST perché le credenziali viaggiano nel corpo, mai nella query string.
            Assert.NotNull(httpPost);
            Assert.Equal(expectedTemplate, httpPost.Template);
        }
    }
}
