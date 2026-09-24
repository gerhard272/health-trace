using HealthTrace.BLL.Services.Interfaces;
namespace HealthTrace.BLL.Models
{
    public class SymptomModel : IModelWithId
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
    }
}