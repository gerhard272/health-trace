using BCrypt.Net;

namespace HealthTrace.BLL.Security
{
    /// <summary>
    /// The salt is generated and embedded in the hash automatically by the package.
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        public bool VerifyPassword(string hashedPassword, string providedPassword)
            => BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
    }
}