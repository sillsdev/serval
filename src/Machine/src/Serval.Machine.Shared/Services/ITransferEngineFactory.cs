namespace Serval.Machine.Shared.Services;

public interface ITransferEngineFactory
{
    ITranslationEngine? Create(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IDetokenizer<string, string> detokenizer,
        ITruecaser truecaser
    );
    void InitNew(string engineDir);
    void Cleanup(string engineDir);
}
