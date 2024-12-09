namespace Serval.Machine.Shared.Services;

public interface IWordAlignmentModelFactory
{
    IWordAlignmentModel Create(string engineDir);
    ITrainer CreateTrainer(string engineDir, ITokenizer<string, int, string> tokenizer, IParallelTextCorpus corpus);
    void InitNew(string engineDir);
    void Cleanup(string engineDir);
    Task UpdateEngineFromAsync(string engineDir, Stream source, CancellationToken cancellationToken = default);
    Task SaveEngineToAsync(string engineDir, Stream destination, CancellationToken cancellationToken = default);
}
