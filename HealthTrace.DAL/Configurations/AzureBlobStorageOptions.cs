using System;
using System.Collections.Generic;
using System.Text;

namespace HealthTrace.DAL.Configurations {
    public class AzureBlobStorageOptions {
        public const string SectionName = "AzureBlobStorage";
        public string ConnectionString { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
    }
}
