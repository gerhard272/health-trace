using System;
using System.Collections.Generic;
using System.Text;
using HealthTrace.DAL.Repositories;

namespace HealthTrace.DAL.Repositories.Interfaces {
    public interface IBlobStorageRepository {
        Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken cancellationToken = default);
        Task<BlobFileResponse?> DownloadAsync(string blobName, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken = default);
    }
}
