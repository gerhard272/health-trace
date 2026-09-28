using System.Text.Json.Serialization;
using HealthTrace.DAL.Entities;

namespace HealthTrace.BLL.Models
{
    public class ExportRequestModel
    {
        public int Id { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ExportStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public List<int> SymptomIds { get; set; } = new();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? FileName { get; set; }
        public string? ErrorMessage { get; set; }
    }
}