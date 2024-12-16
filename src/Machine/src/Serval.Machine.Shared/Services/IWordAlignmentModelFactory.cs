namespace Serval.Machine.Shared.Services;

public interface IWordAlignmentModelFactory : IModelFactory
{
    IWordAlignmentModel Create(string engineDir);
}
