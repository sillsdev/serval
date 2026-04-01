namespace Serval.Translation.Services;

public interface IContractMapper
{
    IReadOnlyList<ParallelCorpusContract> Map(Build build, Engine engine);
}
