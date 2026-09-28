namespace HealthTrace.BLL.Models
{
    public class ExportRequestCreateModel
    {
        public List<int>? SymptomIds { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}