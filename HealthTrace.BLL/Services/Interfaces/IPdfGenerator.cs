using HealthTrace.BLL.Models;

namespace HealthTrace.BLL.Services.Interfaces
{
    public interface IPdfGenerator
    {
        byte[] GenerateSymptomReport(IReadOnlyList<SymptomModel> symptoms, DateTime generatedAtUtc);
    }
}