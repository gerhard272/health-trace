namespace HealthTrace.DAL.Entities
{
    public class ExportRequest : AuditEntity, IEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public ExportStatus Status { get; set; }

        // Selection criteria: symptom ids or date range
        public List<int> SymptomIds { get; set; } = new();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Export output
        public string? BlobName { get; set; }
        public string? FileName { get; set; }

        // Set when status = Failed
        public string? ErrorMessage { get; set; }
    }
}