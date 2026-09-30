using System.ComponentModel.DataAnnotations;

namespace HealthTrace.PL.API.Configurations
{
    /// <summary>
    /// Configurazione del sink su file dei log. La sezione di configurazione
    /// corrispondente si chiama "FileLogging" ed è letta da LoggingSetup.
    /// </summary>
    public class FileLoggingOptions
    {
        public const string SectionName = "FileLogging";

        /// <summary>
        /// Percorso del file di log, relativo alla radice del contenuto dell'applicazione.
        /// Con il rolling giornaliero il nome reale diventa logs/healthtrace-YYYYMMDD.log.
        /// </summary>
        [Required]
        public string Path { get; set; } = "logs/healthtrace-.log";

        /// <summary>
        /// Quanti file ruotati conservare: i più vecchi vengono eliminati.
        /// </summary>
        [Range(1, 365)]
        public int RetainedFileCountLimit { get; set; } = 30;
    }
}