namespace Serval.Machine.Shared.Services;

public interface ITruecaserFactory
{
    ITruecaser Create(string engineDir);
    ITrainer CreateTrainer(string engineDir, ITokenizer<string, int, string> tokenizer, ITextCorpus corpus);
    void Cleanup(string engineDir);
}
