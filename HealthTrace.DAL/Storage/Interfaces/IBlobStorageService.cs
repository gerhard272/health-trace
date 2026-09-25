namespace HealthTrace.DAL.Storage
{
    public interface IBlobStorageService
    {
        Task<string> UploadAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);
        Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
        Uri GetBlobUri(string containerName, string blobName);
    }
}