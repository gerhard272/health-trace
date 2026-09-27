using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using HealthTrace.DAL.Configurations;
using HealthTrace.DAL.Repositories.Interfaces;


namespace HealthTrace.DAL.Repositories {
    public class BlobStorageRepository : IBlobStorageRepository {
        private readonly BlobContainerClient _containerClient;

        public BlobStorageRepository(BlobServiceClient blobServiceClien, IOptions<AzureBlobStorageOptions> options) {
            _containerClient = blobServiceClien.GetBlobContainerClient(options.Value.ContainerName);
        }

        //metodo per uploadare un file su Azure Blob Storage
        public async Task<string> UploadAsync(
            Stream content,
            string blobName,
            string contentType,
            CancellationToken cancellationToken = default) {

            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken); //crealo se non esiste
            var blobClient = _containerClient.GetBlobClient(blobName);

            var uploadOptions = new BlobUploadOptions {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType } //object initializer imposta le proprietà di un oggetto dopo averlo creato 
            };

            await blobClient.UploadAsync(content, uploadOptions, cancellationToken);
            return blobClient.Uri.ToString();
        }

        public async Task<BlobFileResponse?> DownloadAsync(
            string blobName,
            CancellationToken cancellationToken = default) {

            var blobClient = _containerClient.GetBlobClient(blobName);
            if (!await blobClient.ExistsAsync(cancellationToken)) {
                return null;
            }

            try {
                var downloadResponse = await blobClient.DownloadAsync(cancellationToken);
                return new BlobFileResponse {
                    Content = downloadResponse.Value.Content,
                    ContentType = downloadResponse.Value.Details.ContentType,
                    Name = blobName
                };

            } catch (Exception ex) {
                throw new Exception($"Error download blob '{blobName}': {ex.Message}", ex);

            }

        }

        public async Task<bool> DeleteAsync(
            string blobName,
            CancellationToken cancellationToken = default) {
            var blobClient = _containerClient.GetBlobClient(blobName);
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return response.Value;
        }

        public async Task<bool> ExistsAsync(
            string blobName,
            CancellationToken cancellationToken = default) {
            var blobClient = _containerClient.GetBlobClient(blobName);
            return await blobClient.ExistsAsync(cancellationToken);
        }
    }
}
