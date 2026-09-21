
namespace HealthTrace.BLL.Services.Interfaces
{
    /// <summary>
    /// Interfaccia creata pensando che in futuro
    /// potremmo dover gestire una chiave composita
    /// </summary>
    public interface IModelWithId
    {
        int Id { get; }
    }
}
