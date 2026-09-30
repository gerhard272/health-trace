using HealthTrace.BLL.Services.Interfaces;

/// <summary>
/// Output/CRUD DTO: PasswordHash is intentionally excluded so authentication data is never exposed in API responses;
/// it is handled internally by the registration/login services with a dedicated DTO.
/// </summary>
namespace HealthTrace.BLL.Models
{
    /// <summary>
    /// Output/CRUD DTO for user information.
    /// </summary>
    public class UserModel : UserBaseModel, IModelWithId
    {
        public int Id { get; set; }
    }
}