using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using AppValidationException = HealthTrace.BLL.Exceptions.ValidationException;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using HealthTrace.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HealthTrace.PL.API.Security
{
    /// <summary>
    /// Handles Basic Authentication for the application.
    /// Extends AuthenticationHandler and implements the logic to authenticate
    /// users with the credentials provided in the Authorization header of the HTTP request.
    /// </summary>
    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IUserService _userService;

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IUserService userService)
            : base(options, logger, encoder)
        {
            _userService = userService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var authHeader) ||
                !authHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.NoResult();

            try
            {
                var credentialBytes = Convert.FromBase64String(authHeader.Parameter ?? string.Empty);
                var credentials = Encoding.UTF8.GetString(credentialBytes);

                var separatorIndex = credentials.IndexOf(':');
                if (separatorIndex <= 0)
                    return AuthenticateResult.Fail("Invalid credentials");

                var username = credentials[..separatorIndex];
                var password = credentials[(separatorIndex + 1)..];

                UserModel user;
                try
                {
                    user = await _userService.LoginAsync(username, password, Context.RequestAborted);
                }
                catch (AppValidationException ex)
                {
                    // Empty username or password: the header is malformed or the client cannot
                    // send credentials. This is not a failed authentication attempt,
                    // so no Warning and no details in the log.
                    Logger.LogDebug("Basic auth header without usable credentials: {ErrorMessage}", ex.Message);
                    return AuthenticateResult.Fail("Missing credentials");
                }
                catch (UnauthorizedException)
                {
                    // Invalid credentials: an expected event, Warning without stack trace,
                    // consistent with how GlobalExceptionHandler treats 4xx.
                    Logger.LogWarning("Basic authentication failed for {Username}", username);
                    return AuthenticateResult.Fail("Invalid credentials");
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);

                return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
            }
            catch (FormatException)
            {
                return AuthenticateResult.Fail("Invalid credentials");
            }
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers.WWWAuthenticate = "Basic realm=\"HealthTrace\"";
            await base.HandleChallengeAsync(properties);
        }
    }
}