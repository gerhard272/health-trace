
namespace HealthTrace.BLL.Services.Interfaces
{
    /// <summary>
    /// Interface created in case, in the future,
    /// we need to handle a composite key
    /// </summary>
    public interface IModelWithId
    {
        int Id { get; }
    }
}
