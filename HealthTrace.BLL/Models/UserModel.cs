using HealthTrace.BLL.Services.Interfaces;

/// <summary>
/// DTO di output/CRUD: PasswordHash è volutamente escluso per non esporre dati di autenticazione nelle risposte API;
/// verrà gestito internamente dai servizi di registrazione/login con un DTO dedicato.
/// </summary>
namespace HealthTrace.BLL.Models
{
    public class UserModel : IModelWithId
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string CF { get; set; } = string.Empty;
        public DateOnly? BirthDate { get; set; }
        public string? BirthPlace { get; set; }
    }
}