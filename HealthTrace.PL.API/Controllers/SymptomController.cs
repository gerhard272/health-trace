using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrace.PL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] // requires authentication
    public class SymptomController : ControllerBase
    {
        private readonly ISymptomService _service;
        private readonly ICurrentUserService _currentUserService;

        public SymptomController(ISymptomService service, ICurrentUserService currentUserService)
        {
            _service = service;
            _currentUserService = currentUserService;
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/symptom/5

        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SymptomModel>> GetById(
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            var symptom = await _service.GetByIdAsync(userId, id, cancellationToken);
            if (symptom is null)
                throw new NotFoundException("Symptom", id);

            return Ok(symptom);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/symptom
        // GET: api/symptom?date=2026-09-23
        // GET: api/symptom?name=fever
        // Single endpoint for the collection: full list or filter by date or by name.
        // The two filters are mutually exclusive (until the service supports a combined search).

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IReadOnlyList<SymptomModel>>> GetAllSymptoms(
            [FromQuery] DateTime? date,
            [FromQuery] string? name,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            var hasName = !string.IsNullOrWhiteSpace(name);

            if (date.HasValue && hasName)
                throw new BadRequestException("Specify only one of 'date' or 'name'.");

            if (date.HasValue)
                return Ok(await _service.GetByDateAsync(userId, date.Value, cancellationToken));

            if (hasName)
                return Ok(await _service.GetByNameAsync(userId, name!, cancellationToken));

            return Ok(await _service.GetAllByUserIdAsync(userId, cancellationToken));
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // POST: api/symptom
        // The UserId is set by the service from the authenticated user:
        // any value sent in the body is ignored.

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<SymptomModel>> Create(
            [FromBody] SymptomModel symptom,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            var createdSymptom = await _service.CreateAsync(userId, symptom, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = createdSymptom.Id }, createdSymptom);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // PUT: api/symptom/5

        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] int id,
            [FromBody] SymptomModel symptom,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            // Consistency check: the id in the route must match the one in the body
            if (id != symptom.Id)
                throw new BadRequestException("The id in the route does not match the id in the request body.");

            var updated = await _service.UpdateAsync(userId, symptom, cancellationToken);
            if (updated is null)
                throw new NotFoundException("Symptom", id);

            return NoContent();
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // DELETE: api/symptom/5

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            var deleted = await _service.DeleteAsync(userId, id, cancellationToken);
            if (!deleted)
                throw new NotFoundException("Symptom", id);

            return NoContent();
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // Gets the authenticated user id; if the claim is missing the global
        // handler returns 401 without repeating the check in every action.
        private int GetUserId() => _currentUserService.UserId
            ?? throw new UnauthorizedException();
    }
}