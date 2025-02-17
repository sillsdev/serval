namespace Serval.Machine.Shared.Services;

public interface IWordAlignmentModelFactory : IModelFactory
{
    IWordAlignmentModel Create(string engineDir, string? modelType = null);

    ITrainer CreateTrainer(
        string engineDir,
        ITokenizer<string, int, string> tokenizer,
        IParallelTextCorpus corpus,
        string? modelType = null
    );
}
