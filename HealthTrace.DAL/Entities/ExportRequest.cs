namespace HealthTrace.DAL.Entities
{
    public class ExportRequest : AuditEntity, IEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public ExportStatus Status { get; set; }

        // Criteri di selezione Id, range di date
        public List<int> SymptomIds { get; set; } = new();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // valori utili per l'export
        public string? BlobName { get; set; }
        public string? FileName { get; set; }

        // valorizzato se status = failed
        public string? ErrorMessage { get; set; }
    }
}