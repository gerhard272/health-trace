using System;
using System.Collections.Generic;
using System.Text;

namespace HealthTrace.DAL.Repositories {
    public class BlobFileResponse {
        public Stream Content { get; init; } = Stream.Null;
        public string ContentType { get; init; } = "application/octet-stream";
        public string Name { get; init; } = string.Empty;
    }
}
