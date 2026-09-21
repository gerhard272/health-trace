namespace HealthTrace.DAL.Entities
{
	public class User
	{
		public int Id { get; set; }
		public string Username { get; set; } = string.Empty;
		public string PasswordHash { get; set; } = string.Empty;
		public string FirstName { get; set; } = string.Empty;
		public string LastName { get; set; } = string.Empty;
		public string CF { get; set; } = string.Empty;
		public DateOnly? BirthDate { get; set; }
		public string? BirthPlace { get; set; }
	}
}
