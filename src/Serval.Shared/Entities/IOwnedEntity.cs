namespace Serval.Shared.Entities;

public interface IOwnedEntity : IEntity
{
    string Owner { get; set; }
}
