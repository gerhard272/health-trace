using HealthTrace.BLL.Models;

namespace HealthTrace.BLL.Services.Interfaces
{
    /// <summary>
    /// Contratto dell'utente: estende IGenericService, il CRUD generico già
    /// implementato da GenericService e aggiunge RegisterAsync e LoginAsync.
    /// Nota: per gli utenti il Create/Update generico NON va usato (l'hash non transita in UserModel):
    /// la creazione passa esclusivamente da RegisterAsync
    /// il login passa esclusivamente da LoginAsync
    /// Gli esiti negativi non si esprimono con un valore di ritorno: RegisterAsync e
    /// LoginAsync restituiscono il modello oppure lanciano l'eccezione applicativa che
    /// ne descrive l'esito, e sarà ExceptionStatusMapper a tradurla in status code e
    /// corpo ProblemDetails.
    /// </summary>
    public interface IUserService : IGenericService<UserModel>
    {
        /// <exception cref="HealthTrace.BLL.Exceptions.ValidationException">
        /// Modello non valido, username o codice fiscale già registrati.
        /// </exception>
        Task<UserModel> RegisterAsync(RegisterModel model, CancellationToken cancellationToken = default);

        /// <exception cref="HealthTrace.BLL.Exceptions.ValidationException">
        /// Username o password mancanti.
        /// </exception>
        /// <exception cref="HealthTrace.BLL.Exceptions.UnauthorizedException">
        /// Credenziali non valide. Lo stesso esito copre utente inesistente e password
        /// errata, per non rivelare quale dei due è sbagliato.
        /// </exception>
        Task<UserModel> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

        /// <inheritdoc cref="LoginAsync(string, string, CancellationToken)"/>
        Task<UserModel> LoginAsync(LoginModel model, CancellationToken cancellationToken = default);
    }
}