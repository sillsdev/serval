namespace Serval.Machine.Shared.Services;

public class TransferEngineFactory : ITransferEngineFactory
{
    public ITranslationEngine? Create(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IDetokenizer<string, string> detokenizer,
        ITruecaser truecaser
    )
    {
        string hcSrcConfigFileName = Path.Combine(engineDir, "src-hc.xml");
        string hcTrgConfigFileName = Path.Combine(engineDir, "trg-hc.xml");
        ITranslationEngine? transferEngine = null;
        if (File.Exists(hcSrcConfigFileName) && File.Exists(hcTrgConfigFileName))
        {
            var hcTraceManager = new TraceManager();

            Language srcLang = XmlLanguageLoader.Load(hcSrcConfigFileName);
            var srcMorpher = new Morpher(hcTraceManager, srcLang);

            Language trgLang = XmlLanguageLoader.Load(hcTrgConfigFileName);
            var trgMorpher = new Morpher(hcTraceManager, trgLang);

            transferEngine = new TransferEngine(
                srcMorpher,
                new SimpleTransferer(new GlossMorphemeMapper(trgMorpher)),
                trgMorpher
            )
            {
                SourceTokenizer = tokenizer,
                TargetDetokenizer = detokenizer,
                LowercaseSource = true,
                Truecaser = truecaser,
            };
        }
        return transferEngine;
    }

    public void InitNew(string engineDir)
    {
        // TODO: generate source and target config files
    }

    public void Cleanup(string engineDir)
    {
        if (!Directory.Exists(engineDir))
            return;
        string hcSrcConfigFileName = Path.Combine(engineDir, "src-hc.xml");
        if (File.Exists(hcSrcConfigFileName))
            File.Delete(hcSrcConfigFileName);
        string hcTrgConfigFileName = Path.Combine(engineDir, "trg-hc.xml");
        if (File.Exists(hcTrgConfigFileName))
            File.Delete(hcTrgConfigFileName);
        if (!Directory.EnumerateFileSystemEntries(engineDir).Any())
            Directory.Delete(engineDir);
    }
}
