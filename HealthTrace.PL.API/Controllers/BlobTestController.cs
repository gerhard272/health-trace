using HealthTrace.BLL.Exceptions;
using HealthTrace.DAL.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace HealthTrace.PL.API.Controllers
{
    [Authorize] // test endpoints exposed to authenticated users only
    [ApiController]
    [Route("api/test/blob")]
    public class BlobTestController : ControllerBase
    {
        private readonly IBlobStorageService _blobStorageService;

        public BlobTestController(IBlobStorageService blobStorageService)
        {
            _blobStorageService = blobStorageService;
        }

        [HttpPost("write-test")]
        public async Task<IActionResult> WriteTest(CancellationToken cancellationToken)
        {
            var content = "Hello from HealthTrace, blob storage works!";
            var bytes = Encoding.UTF8.GetBytes(content);

            using var stream = new MemoryStream(bytes);

            var uri = await _blobStorageService.UploadAsync(
                containerName: "test-container",
                blobName: "test-file.txt",
                content: stream,
                contentType: "text/plain",
                cancellationToken: cancellationToken);

            return Ok(new { message = "Upload successful", uri });
        }

        [HttpGet("read-test")]
        public async Task<IActionResult> ReadTest(CancellationToken cancellationToken)
        {
            const string blobName = "test-file.txt";

            var exists = await _blobStorageService.ExistsAsync("test-container", blobName, cancellationToken);
            if (!exists)
                throw new NotFoundException("Blob", blobName);

            var stream = await _blobStorageService.DownloadAsync("test-container", blobName, cancellationToken);
            return File(stream, "text/plain", blobName);
        }

        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException("No file received.");

            using var stream = file.OpenReadStream();

            var uri = await _blobStorageService.UploadAsync(
                containerName: "test-container",
                blobName: file.FileName,
                content: stream,
                contentType: file.ContentType,
                cancellationToken: cancellationToken);

            return Ok(new { message = "Upload successful", fileName = file.FileName, uri });
        }

        [HttpDelete("delete-test")]
        public async Task<IActionResult> DeleteTest(CancellationToken cancellationToken)
        {
            var deleted = await _blobStorageService.DeleteAsync("test-container", "test-file.txt", cancellationToken);
            return Ok(new { deleted });
        }
    }
}