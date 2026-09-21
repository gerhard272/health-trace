namespace HealthTrace.DAL.Entities
{
    public class Symptom
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string EventName { get; set; }

        public string? Description { get; set; }

        public DateTime EventDate { get; set; }

    }
}

