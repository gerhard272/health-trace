/*
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;


namespace HealthTrace.PL.API.Controllers {

    [ApiController]
    /// <summary>
    /// [Authorize] attributo indica che tutte le azioni in questo controller richiedono l'autenticazione dell'utente.
    /// Gli utenti non autenticati riceveranno una risposta 401 Unauthorized.
    /// </summary>>
    [Authorize]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] //questo indica che tutte le azioni del controller richiedono l'autenticazione - Mostra errore perché non ho i file bll
    public class SymptomController : ControllerBase {
        private readonly ISymptomService _service;

        public SymptomController(ISymptomService service) {
            _service = service;
        }

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
            int userId, //eliminare ridondanza userid perché usato già in symptom model 
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

*/

//PER PRECAUZIONE HO COMMENTATO IL CODICE SOPRA

using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrace.PL.API.Controllers {

    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize] //protezione sulle auth
    public class SymptomController : ControllerBase {
        private readonly ISymptomService _service;

        public SymptomController(ISymptomService service) {
            _service = service;
        }

        // GET: api/symptom/5?userId=1

        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SymptomModel>> GetById(
            [FromRoute] int id,               // L'identificativo del sintomo estratto direttamente dal percorso URL
            [FromQuery] int userId,
            CancellationToken cancellationToken) {
            var symptom = await _service.GetByIdAsync(userId, id, cancellationToken);

            return symptom is null ? NotFound() : Ok(symptom);
        }

        // GET: api/symptom?userId=1
        // GET: api/symptom?userId=1&date=2026-09-23
        // GET: api/symptom?userId=1&name=febbre
        // Endpoint unico per la collezione: gestisce sia il recupero di lista sia i filtri (data o nome)
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<SymptomModel>>> GetAllSymptoms(
            [FromQuery] int userId,
            [FromQuery] DateTime? date,
            [FromQuery] string? name,
            CancellationToken cancellationToken) {
            // Se è stata specificata una data, interroga il metodo del service dedicato
            if (date.HasValue) {
                var symptomsByDate = await _service.GetByDateAsync(userId, date.Value, cancellationToken);
                return Ok(symptomsByDate);
            }

            // Se è stato specificato un nome valido (non nullo e non composto da soli spazi)
            if (!string.IsNullOrWhiteSpace(name)) {
                var symptomsByName = await _service.GetByNameAsync(userId, name, cancellationToken);
                return Ok(symptomsByName);
            }

            // Se non sono presenti filtri aggiuntivi, estrae tutti i sintomi dell'utente
            var allSymptoms = await _service.GetAllByUserIdAsync(userId, cancellationToken);
            return Ok(allSymptoms);
        }

        // POST: api/symptom

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SymptomModel>> Create(
            [FromBody] SymptomModel symptom,
            CancellationToken cancellationToken) {
            // Il UserId viene recuperato direttamente dalla proprietà dell'oggetto passato
            var createdSymptom = await _service.CreateAsync(symptom.UserId, symptom, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = createdSymptom.Id, userId = createdSymptom.UserId }, createdSymptom);
        }

        // PUT: api/symptom/5
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] int id,
            [FromBody] SymptomModel symptom,
            CancellationToken cancellationToken) {
            // Controllo di coerenza: l'ID nella rotta deve coincidere con l'ID nell'oggetto inviato
            if (id != symptom.Id) {
                return BadRequest(); // Restituisce 400 Bad Request se non coincidono
            }

            var update = await _service.UpdateAsync(symptom.UserId, symptom, cancellationToken);

            // Se update è null, il record da modificare non esiste: 404 Not Found. Altrimenti 204 No Content per standard REST
            return update is null ? NotFound() : NoContent();
        }

        // DELETE: api/symptom/5?userId=1

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            [FromRoute] int id,
            [FromQuery] int userId,
            CancellationToken cancellationToken) {
            var deleted = await _service.DeleteAsync(userId, id, cancellationToken);

            // Se l'eliminazione è andata a buon fine restituisce 204 No Content, altrimenti 404 Not Found
            if (deleted) {
                return NoContent();
            }

            return NotFound();
        }
    }
}

