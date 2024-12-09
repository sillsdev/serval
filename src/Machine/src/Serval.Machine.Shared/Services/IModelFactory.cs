namespace Serval.Machine.Shared.Services;

public interface IModelFactory
{
    ITrainer CreateTrainer(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IParallelTextCorpus corpus
    );

    void InitNew(string engineDir);
    void Cleanup(string engineDir);
    Task UpdateEngineFromAsync(string engineDir, Stream source, CancellationToken cancellationToken = default);
    Task SaveEngineToAsync(string engineDir, Stream destination, CancellationToken cancellationToken = default);
}
