namespace Serval.Translation.Services;

public interface ICorpusMappingService
{
    IReadOnlyList<FilteredParallelCorpus> Map(Build build, Engine engine);
}
