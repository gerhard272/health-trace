using Microsoft.AspNetCore.Mvc;

namespace HealthTrace.PL.API.Handlers.Interfaces
{
    /// <summary>
    /// Trasforma l'eccezione intercettata dal gestore globale nel ProblemDetails
    /// (RFC 9457) restituito al client: è l'unico punto dell'applicazione in cui il
    /// tipo di eccezione viene associato a uno status code HTTP, così i servizi del
    /// BLL restano ignari dell'HTTP e i controller non devono mappare eccezioni.
    /// Le eccezioni che derivano da AppException dichiarano nel messaggio un
    /// contratto destinato al body della risposta; ogni altra eccezione è un errore
    /// interno e non deve esporre dettagli.
    /// </summary>
    public interface IErrorDetailsMapper
    {
        /// <summary>
        /// Restituisce i dati del problema corrispondenti all'eccezione ricevuta.
        /// </summary>
        ProblemDetails Map(Exception exception);
    }
}