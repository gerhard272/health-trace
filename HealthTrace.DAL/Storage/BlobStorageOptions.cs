using System.ComponentModel.DataAnnotations;

namespace HealthTrace.DAL.Storage
{
    public class BlobStorageOptions
    {
        public const string SectionName = "BlobStorage";

        [Required]
        public string ConnectionString { get; set; } = string.Empty;

        [Required]
        public string DefaultContainerName { get; set; } = "exports";

        [Range(1, long.MaxValue)]
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    }
}