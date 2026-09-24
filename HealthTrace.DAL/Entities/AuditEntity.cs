namespace HealthTrace.DAL.Entities
{
    public abstract class AuditEntity
    {
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }
        public int? ModifiedBy { get; set; }

        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }

        public bool IsDeleted { get; set; }
    }
}