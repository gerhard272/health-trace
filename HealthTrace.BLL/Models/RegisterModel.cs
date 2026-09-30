namespace HealthTrace.BLL.Models
{
    /// <summary>
    /// Input DTO for registering a new user.
    /// </summary>
    public class RegisterModel : UserBaseModel
    {
        public string Password { get; set; } = string.Empty;
        public string PasswordConfirmation { get; set; } = string.Empty;
    }
}