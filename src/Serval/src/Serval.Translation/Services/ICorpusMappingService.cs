namespace Serval.Translation.Services;

public interface ICorpusMappingService
{
    IReadOnlyList<SIL.ServiceToolkit.Models.ParallelCorpus> Map(Build build, Engine engine);
}
