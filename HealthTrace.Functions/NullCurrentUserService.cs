using HealthTrace.DAL;

namespace HealthTrace.Functions;

// La Function non ha HttpContext: nessun utente autenticato nel senso della richiesta web.
// L'audit (CreatedBy/ModifiedBy) risultera' 0 ("sistema") per le scritture fatte da qui.
public class NullCurrentUserService : ICurrentUserService
{
    public int? UserId => null;
}