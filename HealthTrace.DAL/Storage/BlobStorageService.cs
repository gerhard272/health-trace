using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace HealthTrace.DAL.Storage
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobStorageOptions _options;

        public BlobStorageService(IOptions<BlobStorageOptions> options)
        {
            _options = options.Value;
            _blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        }

        public async Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            if (content.Length > _options.MaxFileSizeBytes)
                throw new InvalidOperationException($"The file has exceeded the maximum allowed size of {_options.MaxFileSizeBytes} byte.");

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = contentType
            }, cancellationToken: cancellationToken);

            return blobClient.Uri.ToString();
        }

        public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }

        public async Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return response.Value;
        }

        public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
        {
            var blobClient = _blobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobName);
            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }

        public Uri GetBlobUri(string containerName, string blobName)
        {
            return _blobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobName).Uri;
        }
    }
}