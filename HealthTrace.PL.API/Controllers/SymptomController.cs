using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using HealthTrace.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrace.PL.API.Controllers {

    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] //protezione sulle auth
    public class SymptomController : ControllerBase {
        private readonly ISymptomService _service;
        private readonly ICurrentUserService _currentUserService;

        public SymptomController(ISymptomService service, ICurrentUserService currentUserService) {
            _service = service;
            _currentUserService = currentUserService;
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/symptom/5?userId=1

        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SymptomModel>> GetById(
            [FromRoute] int id, 
            CancellationToken cancellationToken) {

            if(_currentUserService.UserId is null) {
                return Unauthorized(); // Se l'utente non è autenticato, restituisce 401 Unauthorized
            }
            var symptom = await _service.GetByIdAsync(_currentUserService.UserId.Value, id, cancellationToken);
            return symptom is null ? NotFound() : Ok(symptom);
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // GET: api/symptom?userId=1
        // GET: api/symptom?userId=1&date=2026-09-23
        // GET: api/symptom?userId=1&name=febbre
        // Endpoint unico per la collezione: gestisce sia il recupero di lista sia i filtri (data o nome)

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<SymptomModel>>> GetAllSymptoms(
            
            [FromQuery] DateTime? date,
            [FromQuery] string? name,
            CancellationToken cancellationToken) {

            if(_currentUserService.UserId is null) {
                return Unauthorized(); // Se l'utente non è autenticato, restituisce 401 Unauthorized
            }

            // Se è presente il filtro per data, interroga il metodo dedicato
            if (date.HasValue) {
                var symptomsByDate = await _service.GetByDateAsync(_currentUserService.UserId.Value, date.Value, cancellationToken);
                return Ok(symptomsByDate);
            }

            //se è presente il filtro per nome, viene eseguita la ricerca per nome
            if(!string.IsNullOrEmpty(name)) {
                var symptomsByName = await _service.GetByNameAsync(_currentUserService.UserId.Value, name, cancellationToken);
                return Ok(symptomsByName);
            }
            //recupera la lista completa dei sintomi per l'utente autenticato
            var allSymptoms = await _service.GetAllByUserIdAsync(_currentUserService.UserId.Value, cancellationToken);
            return Ok(allSymptoms);

        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // POST: api/symptom

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<SymptomModel>> Create(
            [FromBody] SymptomModel symptom,
            CancellationToken cancellationToken) {
            if(_currentUserService.UserId is null) {
                return Unauthorized(); // Se l'utente non è autenticato, restituisce 401 Unauthorized
            }
            int userId = _currentUserService.UserId.Value;
            symptom.UserId = userId; // Associa l'ID dell'utente autenticato al sintomo

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
            CancellationToken cancellationToken) {
            // Controllo di coerenza: l'ID nella rotta deve coincidere con l'ID nell'oggetto inviato
            if (_currentUserService.UserId is null) {
                return Unauthorized();
            }

            // Verifica corrispondenza tra l'ID nella rotta e l'ID nel corpo della richiesta
            if (id != symptom.Id) {
                return BadRequest();
            }

            int userId = _currentUserService.UserId.Value;

            // Associa l'ID dell'utente autenticato al modello per l'aggiornamento
            symptom.UserId = userId;

            var update = await _service.UpdateAsync(userId, symptom, cancellationToken);

            return update is null ? NotFound() : NoContent();
        
        }

        ////////////////////////////////////////////////////////////////////////////////////////

        // DELETE: api/symptom/5?userId=1

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(
            [FromRoute] int id,
            CancellationToken cancellationToken) {
            
            if(_currentUserService.UserId is null) {
                return Unauthorized(); // Se l'utente non è autenticato, restituisce 401 Unauthorized
            }

            var delete = await _service.DeleteAsync(_currentUserService.UserId.Value, id, cancellationToken);

            if (delete) {
                return NoContent();
            }
            return NotFound();
        }
    }
}

