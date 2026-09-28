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
        /// Registra un nuovo utente. Gli esiti negativi non transitano da qui: il
        /// servizio lancia l'eccezione applicativa, tradotta in ProblemDetails dal
        /// gestore globale delle eccezioni.
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
        /// Verifica le credenziali e restituisce il profilo utente. Le credenziali non
        /// valide producono un 401 ProblemDetails tramite UnauthorizedException, senza
        /// rivelare se è stato l'utente a non esistere o la password a essere sbagliata.
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