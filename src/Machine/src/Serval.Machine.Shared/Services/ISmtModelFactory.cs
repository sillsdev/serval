namespace Serval.Machine.Shared.Services;

public interface ISmtModelFactory : IModelFactory
{
    ITrainer CreateTrainer(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IParallelTextCorpus corpus
    );

    IInteractiveTranslationModel Create(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IDetokenizer<string, string> detokenizer,
        ITruecaser truecaser
    );
}
