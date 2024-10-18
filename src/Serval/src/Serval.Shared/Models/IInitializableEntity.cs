namespace Serval.Shared.Models;

public interface IInitializableEntity : IEntity
{
    bool? IsInitialized { get; set; }
    DateTime? DateCreated { get; set; }
}
