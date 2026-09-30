using HealthTrace.BLL.Models;

namespace HealthTrace.BLL.Services.Interfaces
{
    /// <summary>
    /// User contract: extends IGenericService, the generic CRUD already
    /// implemented by GenericService, and adds RegisterAsync and LoginAsync.
    /// Note: the generic Create/Update must NOT be used for users (the hash does not travel in UserModel):
    /// users are created only through RegisterAsync
    /// and log in only through LoginAsync.
    /// Failures are not expressed as return values: RegisterAsync and
    /// LoginAsync return the model or throw the application exception that
    /// describes the outcome, and ExceptionStatusMapper translates it into a status code and
    /// ProblemDetails body.
    /// </summary>
    public interface IUserService : IGenericService<UserModel>
    {
        /// <exception cref="HealthTrace.BLL.Exceptions.ValidationException">
        /// Invalid model, username or fiscal code already registered.
        /// </exception>
        Task<UserModel> RegisterAsync(RegisterModel model, CancellationToken cancellationToken = default);

        /// <exception cref="HealthTrace.BLL.Exceptions.ValidationException">
        /// Missing username or password.
        /// </exception>
        /// <exception cref="HealthTrace.BLL.Exceptions.UnauthorizedException">
        /// Invalid credentials. The same outcome covers a non-existent user and a wrong
        /// password, so as not to reveal which of the two is wrong.
        /// </exception>
        Task<UserModel> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

        /// <inheritdoc cref="LoginAsync(string, string, CancellationToken)"/>
        Task<UserModel> LoginAsync(LoginModel model, CancellationToken cancellationToken = default);
    }
}