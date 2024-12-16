namespace Serval.Machine.Shared.Services;

public interface ISmtModelFactory : IModelFactory
{
    IInteractiveTranslationModel Create(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IDetokenizer<string, string> detokenizer,
        ITruecaser truecaser
    );
}
