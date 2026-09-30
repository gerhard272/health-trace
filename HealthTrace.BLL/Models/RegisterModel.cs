namespace HealthTrace.BLL.Models
{
    /// <summary>
    /// DTO di input per la registrazione di un nuovo utente.
    /// </summary>
    public class RegisterModel : UserBaseModel
    {
        public string Password { get; set; } = string.Empty;
        public string PasswordConfirmation { get; set; } = string.Empty;
    }
}