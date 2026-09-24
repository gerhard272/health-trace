using System;
using System.Collections.Generic;
using System.Text;

namespace HealthTrace.DAL
{
    public interface ICurrentUserService
    {
        int? UserId { get; }
    }
}
