namespace HealthTrace.BLL.Security
{
    /// <summary>
    /// Password hashing contract (dependency inversion principle):
    /// the BLL depends on this abstraction, implementations (e.g. BCrypt) are
    /// interchangeable without changing UserService.
    /// </summary>
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string hashedPassword, string providedPassword);
    }
}