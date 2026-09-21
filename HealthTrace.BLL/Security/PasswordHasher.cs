using BCrypt.Net;

namespace HealthTrace.BLL.Security
{
	/// <summary>
	/// Il salt viene generato e incorporato nell'hash automaticamente dal pacchetto.
	/// </summary>
	public class PasswordHasher : IPasswordHasher
	{
		public string HashPassword(string password) => BCrypt.HashPassword(password);

		public bool VerifyPassword(string hashedPassword, string providedPassword)
			=> BCrypt.Verify(providedPassword, hashedPassword);
	}
}