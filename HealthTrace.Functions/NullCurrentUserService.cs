using HealthTrace.DAL;

namespace HealthTrace.Functions;

// The Function has no HttpContext, so there is no authenticated user.
// Audit fields (CreatedBy/ModifiedBy) are 0 ("system") for writes made from here.
public class NullCurrentUserService : ICurrentUserService
{
    public int? UserId => null;
}