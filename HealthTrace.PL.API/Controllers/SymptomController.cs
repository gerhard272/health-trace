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
    [Authorize] // protezione sulle auth
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
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var symptom = await _service.GetByIdAsync(userId, id, cancellationToken);
            return symptom is null ? NotFound() : Ok(symptom);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/symptom
        // GET: api/symptom?date=2026-09-23
        // GET: api/symptom?name=febbre
        // Endpoint unico per la collezione: lista completa oppure filtro per data o per nome.
        // I due filtri sono mutuamente esclusivi (finche' non esiste una ricerca combinata nel service).

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IReadOnlyList<SymptomModel>>> GetAllSymptoms(
            [FromQuery] DateTime? date,
            [FromQuery] string? name,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var hasName = !string.IsNullOrWhiteSpace(name);

            if (date.HasValue && hasName)
                return BadRequest("Specificare solo uno tra 'date' e 'name'.");

            if (date.HasValue)
                return Ok(await _service.GetByDateAsync(userId, date.Value, cancellationToken));

            if (hasName)
                return Ok(await _service.GetByNameAsync(userId, name!, cancellationToken));

            return Ok(await _service.GetAllByUserIdAsync(userId, cancellationToken));
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // POST: api/symptom
        // L'UserId viene imposto dal service partendo dall'utente autenticato:
        // eventuali valori inviati nel body vengono ignorati.

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<SymptomModel>> Create(
            [FromBody] SymptomModel symptom,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

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
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            // Controllo di coerenza: l'id nella rotta deve coincidere con quello nel body
            if (id != symptom.Id)
                return BadRequest("L'id nella rotta non coincide con l'id nel corpo della richiesta.");

            var updated = await _service.UpdateAsync(userId, symptom, cancellationToken);
            return updated is null ? NotFound() : NoContent();
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
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var deleted = await _service.DeleteAsync(userId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // Centralizza il controllo sull'utente autenticato, invece di ripeterlo in ogni action
        private bool TryGetUserId(out int userId)
        {
            var current = _currentUserService.UserId;
            userId = current ?? 0;
            return current.HasValue;
        }
    }
}