using HealthTrace.BLL.Models;
using HealthTrace.BLL.Results;
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

        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserModel>> Register(
            [FromBody] RegisterModel model,
            CancellationToken cancellationToken)
        {
            var result = await _userService.RegisterAsync(model, cancellationToken);

            return result.Type switch
            {
                ServiceResultType.Success => CreatedAtAction(nameof(Register), new { }, result.Data),
                ServiceResultType.Unauthorized => Unauthorized(),
                _ => ValidationProblem(new ValidationProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed",
                    Errors = { ["general"] = result.Errors.ToArray() }
                })
            };
        }
    }
}