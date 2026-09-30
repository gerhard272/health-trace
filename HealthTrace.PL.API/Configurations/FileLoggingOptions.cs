using System.ComponentModel.DataAnnotations;

namespace HealthTrace.PL.API.Configurations
{
    /// <summary>
    /// Configuration of the file log sink. The matching configuration
    /// section is called "FileLogging" and is read by LoggingSetup.
    /// </summary>
    public class FileLoggingOptions
    {
        public const string SectionName = "FileLogging";

        /// <summary>
        /// Log file path, relative to the application content root.
        /// With daily rolling the actual name becomes logs/healthtrace-YYYYMMDD.log.
        /// </summary>
        [Required]
        public string Path { get; set; } = "logs/healthtrace-.log";

        /// <summary>
        /// How many rolled files to keep: older ones are deleted.
        /// </summary>
        [Range(1, 365)]
        public int RetainedFileCountLimit { get; set; } = 30;
    }
}