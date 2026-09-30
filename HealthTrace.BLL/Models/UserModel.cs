using HealthTrace.BLL.Services.Interfaces;

/// <summary>
/// DTO di output/CRUD: PasswordHash è volutamente escluso per non esporre dati di autenticazione nelle risposte API;
/// verrà gestito internamente dai servizi di registrazione/login con un DTO dedicato.
/// </summary>
namespace HealthTrace.BLL.Models
{
    /// <summary>
    /// DTO di output/CRUD per le informazioni dell'utente.
    /// </summary>
    public class UserModel : UserBaseModel, IModelWithId
    {
        public int Id { get; set; }
    }
}