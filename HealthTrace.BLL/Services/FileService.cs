using HealthTrace.DAL.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using HealthTrace.DAL.Repositories;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.BLL.Services.Interfaces;

namespace HealthTrace.BLL.Services {
    public class FileService : IFileService {

        private readonly IBlobStorageRepository _blobRepository;

        public FileService(IBlobStorageRepository blobRepository) {
            _blobRepository = blobRepository;
        }

        public async Task<string> UploadFileAsync(
            Stream fileStream, 
            string originalFileName, 
            string contentType, 
            CancellationToken cancellationToken = default) {

            var fileExtension = Path.GetExtension(originalFileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            return await _blobRepository.UploadAsync(
                fileStream, 
                uniqueFileName, 
                contentType, 
                cancellationToken);
        }

        public async Task<BlobFileResponse?> GetFileAsync(
            string fileName, 
            CancellationToken cancellationToken = default) {
            return await _blobRepository.DownloadAsync(
                fileName, 
                cancellationToken);
        }

        public async Task<bool> DeleteFileAsync(
            string fileName, 
            CancellationToken cancellationToken = default) {
            return await _blobRepository.DeleteAsync(
                fileName,
                cancellationToken);
        }

    }
}
