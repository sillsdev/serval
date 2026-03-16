namespace Serval.Translation.Services;

public interface ICorpusMappingService
{
    SIL.ServiceToolkit.Models.ParallelCorpus Map(ParallelCorpus parallelCorpus);
    SIL.ServiceToolkit.Models.ParallelCorpus Map(Corpus corpus, Engine engine);
    string GetFilePath(string filename);
}
