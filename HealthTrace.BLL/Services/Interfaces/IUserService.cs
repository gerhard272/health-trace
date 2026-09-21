using HealthTrace.BLL.Models;
using HealthTrace.BLL.Results;

namespace HealthTrace.BLL.Services.Interfaces
{
    /// <summary>
    /// Contratto dell'utente: estende IGenericService, il CRUD generico già
    /// implementato da GenericService e aggiunge RegisterAsync.
    /// Nota: per gli utenti il Create/Update generico NON va usato (l'hash non transita in UserModel):
    /// la creazione passa esclusivamente da RegisterAsync.
    /// </summary>
    public interface IUserService : IGenericService<UserModel>
    {
        Task<ServiceResult<UserModel>> RegisterAsync(RegisterModel model, CancellationToken cancellationToken = default);
    }
}