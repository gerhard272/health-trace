using System;
using System.Collections.Generic;
using System.Text;
using HealthTrace.DAL.Repositories;

namespace HealthTrace.BLL.Services.Interfaces {
    public interface IFileService {
        Task<string> UploadFileAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken cancellationToken = default);
        Task<BlobFileResponse?> GetFileAsync(string fileName, CancellationToken cancellationToken = default);
        Task<bool> DeleteFileAsync(string fileName, CancellationToken cancellationToken = default);
    }
}
