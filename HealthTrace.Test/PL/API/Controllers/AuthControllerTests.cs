using System.Reflection;
using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
// This file's namespace mirrors that of the code under test, so the
// nested namespace resolution rule does not find the controller on its own:
// the explicit declaration is needed.
using HealthTrace.PL.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using AppValidationException = HealthTrace.BLL.Exceptions.ValidationException;

namespace HealthTrace.Test.PL.API.Controllers
{
    /// <summary>
    /// Test class for AuthController. The controller has no logic of its own:
    /// it delegates to IUserService and turns the successful outcome into an ActionResult. The tests
    /// therefore check three things: the status code and body of the success response,
    /// that the model and CancellationToken are passed unchanged to the service, and
    /// that application exceptions are not caught here, because translating them
    /// into ProblemDetails is the global handler's job (ExceptionStatusMapper).
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

            // The body is exactly the model returned by the service, no copies.
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
            // Registration must not go through any other service method,
            // in particular not through the generic Create, which does not handle the hash.
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

            // The controller must not catch the exception nor turn it into a
            // BadRequest: translation is the global handler's job.
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

            // The controller uses the LoginModel overload: the username/password one
            // must not be called directly.
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

        // --- Routing and authorization attributes ---

        [Fact]
        public void Controller_IsAnonymousApiControllerWithAuthRoute()
        {
            var type = typeof(AuthController);

            // Register and Login must be reachable without authentication:
            // without AllowAnonymous no user could ever log in.
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

            // POST because credentials travel in the body, never in the query string.
            Assert.NotNull(httpPost);
            Assert.Equal(expectedTemplate, httpPost.Template);
        }
    }
}
