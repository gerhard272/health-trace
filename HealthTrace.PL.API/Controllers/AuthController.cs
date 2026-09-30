using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace HealthTrace.PL.API.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService) => _userService = userService;

        /// <summary>
        /// Registers a new user. Failures do not go through here: the
        /// service throws the application exception, which the global exception
        /// handler translates into ProblemDetails.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(UserModel), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserModel>> Register(
            [FromBody] RegisterModel model,
            CancellationToken cancellationToken)
        {
            var user = await _userService.RegisterAsync(model, cancellationToken);
            return CreatedAtAction(nameof(Register), new { }, user);
        }

        /// <summary>
        /// Checks the credentials and returns the user profile. Invalid credentials
        /// produce a 401 ProblemDetails through UnauthorizedException, without
        /// revealing whether the user does not exist or the password is wrong.
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(UserModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserModel>> Login(
            [FromBody] LoginModel model,
            CancellationToken cancellationToken)
        {
            var user = await _userService.LoginAsync(model, cancellationToken);
            return Ok(user);
        }
    }
}