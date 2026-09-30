using System;
using System.Collections.Generic;
using System.Text;

namespace HealthTrace.BLL.Models
{
    /// <summary>
    /// DTO di base per le informazioni dell'utente
    /// classe base per altri modelli di utenti.
    /// </summary>
    public abstract class UserBaseModel
    {
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string CF { get; set; } = string.Empty;
        public DateOnly? BirthDate { get; set; }
        public string? BirthPlace { get; set; }
    }
}
