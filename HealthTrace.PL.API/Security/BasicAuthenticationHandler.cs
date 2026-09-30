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
    /// Gestisce l'autenticazione di base (Basic Authentication) per l'applicazione.
    /// Estende la classe AuthenticationHandler e implementa la logica per autenticare
    /// gli utenti utilizzando le credenziali fornite nell'header Authorization della richiesta HTTP.
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
                    // Username o password vuoti: l'header è malformato o il client non sa
                    // inviare credenziali. Non è un tentativo di autenticazione fallito,
                    // quindi niente Warning e nessun dettaglio in log.
                    Logger.LogDebug("Basic auth header without usable credentials: {ErrorMessage}", ex.Message);
                    return AuthenticateResult.Fail("Missing credentials");
                }
                catch (UnauthorizedException)
                {
                    // Credenziali non valide: evento atteso, Warning senza stack trace,
                    // coerente con il trattamento dei 4xx in GlobalExceptionHandler.
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