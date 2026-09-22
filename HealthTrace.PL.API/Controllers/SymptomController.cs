using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;


namespace HealthTrace.PL.API.Controllers {

    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] //questo indica che tutte le azioni del controller richiedono l'autenticazione - Mostra errore perché non ho i file bll
    public class SymptomController : ControllerBase {
        private readonly ISymptomService _service;

        public SymptomController(ISymptomService service) {
            _service = service;
        }

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value); //Metodo helper privato: legge l'userId dal claim invece che dalla route - FIX 

        // GET: api/symptoms

        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SymptomModel>> GetById(
            int userId, 
            int symptomId, 
            CancellationToken cancellationToken) {
            var symptom = await _service.GetByIdAsync(userId, symptomId, cancellationToken);
            return symptom is null ? NotFound() : Ok(symptom);

        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IReadOnlyList<SymptomModel>>> GetAll(
            int userId, 
            CancellationToken cancelationToken) {
            return Ok(await _service.GetAllByUserIdAsync(userId, cancelationToken));
        }

        // POST: api/symptoms

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SymptomModel>> Create(
            int userId, 
            SymptomModel symptom, 
            CancellationToken cancellationToken) {
            var createdSymptom = await _service.CreateAsync(userId, symptom, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { userId, id = createdSymptom.Id }, createdSymptom);
        }

        // PUT: api/symptoms/{id}

        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        public async Task<IActionResult> Update(
            int userId, 
            int symptomId, 
            SymptomModel symptom, 
            CancellationToken cancellationToken) {
            if(symptomId != symptom.Id) {
                return BadRequest(); //restituisce 400 Bad Request se l'id nella route non corrisponde all'id nel corpo della richiesta
            }

            var update = await _service.UpdateAsync(userId, 
                symptom, 
                cancellationToken);
            return update is null ? NotFound() : NoContent();

        }

        // DELETE: api/symptoms/{id}

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            int userId, 
            int symptomId, 
            CancellationToken cancellationToken) {
            var deleted = await _service.DeleteAsync(userId, symptomId, cancellationToken);
            if (deleted) {
                return NoContent(); //restituisce 204 No Content per convenzione RESTful
            } 

            return NotFound();
            
        }

        [HttpGet("user/{userId:int}/by-date/{date}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        public async Task<ActionResult<IReadOnlyList<SymptomModel>>> GetByDate(
            int userId,
            DateTime date,
            CancellationToken cancellationToken) {
            var symptoms = await _service.GetByDateAsync(userId, date, cancellationToken);
            return Ok(symptoms);
        }

        [HttpGet("user/{userId}/by-name/{eventName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IReadOnlyList<SymptomModel>>> GetByName(
            int userId,
            string eventName,
            CancellationToken cancellationToken) {
            var symptoms = await _service.GetByNameAsync(userId, eventName, cancellationToken);
            return Ok(symptoms);
        }
    }

}
