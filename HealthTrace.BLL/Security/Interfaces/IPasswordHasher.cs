namespace HealthTrace.BLL.Security
{
    /// <summary>
    /// Contratto di hashing password (principio di inversione delle dipendenze):
    /// il BLL dipende da questa astrazione, le implementazioni (es. BCrypt) sono
    /// intercambiabili senza modificare UserService.
    /// </summary>
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string hashedPassword, string providedPassword);
    }
}