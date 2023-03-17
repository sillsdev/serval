namespace Serval.Shared.Models;

public interface IOwnedEntity : IEntity
{
    string Owner { get; set; }
}
