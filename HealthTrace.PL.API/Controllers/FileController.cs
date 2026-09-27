using Microsoft.AspNetCore.Mvc;
using HealthTrace.BLL.Services.Interfaces;

namespace HealthTrace.PL.API.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase {
        private readonly IFileService _fileService;

        public FilesController(IFileService fileService) {
            _fileService = fileService;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken) {
            if (file is null || file.Length == 0) {
                return BadRequest("Nessun file selezionato o file vuoto.");
            }

            await using var stream = file.OpenReadStream();

            var fileUri = await _fileService.UploadFileAsync(
                stream,
                file.FileName,
                file.ContentType,
                cancellationToken);

            return Ok(new { Url = fileUri });
        }

        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> Download(string fileName, CancellationToken cancellationToken) {
            var fileResponse = await _fileService.GetFileAsync(fileName, cancellationToken);

            if (fileResponse is null) {
                return NotFound($"File '{fileName}' non trovato.");
            }

            // Restituisce lo stream direttamente al client senza caricarlo in memoria
            return File(fileResponse.Content, fileResponse.ContentType, fileResponse.Name);
        }

        [HttpDelete("{fileName}")]
        public async Task<IActionResult> Delete(string fileName, CancellationToken cancellationToken) {
            var deleted = await _fileService.DeleteFileAsync(fileName, cancellationToken);

            if (!deleted) {
                return NotFound($"File '{fileName}' non trovato o già rimosso.");
            }

            return NoContent();
        }
    }
}
