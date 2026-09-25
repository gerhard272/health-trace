using HealthTrace.DAL.Storage;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace HealthTrace.PL.API.Controllers
{
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
            var content = "Hello from HealthTrace, blob storage funziona!";
            var bytes = Encoding.UTF8.GetBytes(content);

            using var stream = new MemoryStream(bytes);

            var uri = await _blobStorageService.UploadAsync(
                containerName: "test-container",
                blobName: "test-file.txt",
                content: stream,
                contentType: "text/plain",
                cancellationToken: cancellationToken);

            return Ok(new { message = "Upload riuscito", uri });
        }

        [HttpGet("read-test")]
        public async Task<IActionResult> ReadTest(CancellationToken cancellationToken)
        {
            var exists = await _blobStorageService.ExistsAsync("test-container", "test-file.txt", cancellationToken);

            if (!exists)
                return NotFound("Il blob di test non esiste, esegui prima write-test");

            var stream = await _blobStorageService.DownloadAsync("test-container", "test-file.txt", cancellationToken);
            return File(stream, "text/plain", "test-file.txt");
        }

        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nessun file ricevuto");

            using var stream = file.OpenReadStream();

            var uri = await _blobStorageService.UploadAsync(
                containerName: "test-container",
                blobName: file.FileName,
                content: stream,
                contentType: file.ContentType,
                cancellationToken: cancellationToken);

            return Ok(new { message = "Upload riuscito", fileName = file.FileName, uri });
        }

        [HttpDelete("delete-test")]
        public async Task<IActionResult> DeleteTest(CancellationToken cancellationToken)
        {
            var deleted = await _blobStorageService.DeleteAsync("test-container", "test-file.txt", cancellationToken);
            return Ok(new { deleted });
        }
    }
}