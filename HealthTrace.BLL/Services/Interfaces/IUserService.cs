using HealthTrace.BLL.Models;
using HealthTrace.BLL.Results;

namespace HealthTrace.BLL.Services.Interfaces
{
    /// <summary>
    /// Contratto dell'utente: estende IGenericService, il CRUD generico già
    /// implementato da GenericService e aggiunge RegisterAsync e LoginAsync.
    /// Nota: per gli utenti il Create/Update generico NON va usato (l'hash non transita in UserModel):
    /// la creazione passa esclusivamente da RegisterAsync
    /// il login passa esclusivamente da LoginAsync
    /// </summary>
    public interface IUserService : IGenericService<UserModel>
    {
        Task<ServiceResult<UserModel>> RegisterAsync(RegisterModel model, CancellationToken cancellationToken = default);

        Task<ServiceResult<UserModel>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

        Task<ServiceResult<UserModel>> LoginAsync(LoginModel model, CancellationToken cancellationToken = default);
    }
}