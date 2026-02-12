namespace Serval.Machine.Shared.Services;

public class ThotSmtModelFactory(IOptionsMonitor<ThotSmtModelOptions> options) : ModelFactoryBase, ISmtModelFactory
{
    private readonly IOptionsMonitor<ThotSmtModelOptions> _options = options;

    public IInteractiveTranslationModel Create(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IDetokenizer<string, string> detokenizer,
        ITruecaser truecaser
    )
    {
        string smtConfigFileName = Path.Combine(engineDir, "smt.cfg");
        IInteractiveTranslationModel model = new ThotSmtModel(ThotWordAlignmentModelType.Hmm, smtConfigFileName)
        {
            SourceTokenizer = tokenizer,
            TargetTokenizer = tokenizer,
            TargetDetokenizer = detokenizer,
            LowercaseSource = true,
            LowercaseTarget = true,
            Truecaser = truecaser,
        };
        return model;
    }

    public override ITrainer CreateTrainer(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IParallelTextCorpus corpus
    )
    {
        string smtConfigFileName = Path.Combine(engineDir, "smt.cfg");
        ITrainer trainer = new ThotSmtModelTrainer(ThotWordAlignmentModelType.Hmm, corpus, smtConfigFileName)
        {
            SourceTokenizer = tokenizer,
            TargetTokenizer = tokenizer,
            LowercaseSource = true,
            LowercaseTarget = true,
        };
        return trainer;
    }

    public override void InitNew(string engineDir)
    {
        if (!Directory.Exists(engineDir))
            Directory.CreateDirectory(engineDir);
        ZipFile.ExtractToDirectory(_options.CurrentValue.NewModelFile, engineDir);
    }

    public override void Cleanup(string engineDir)
    {
        if (!Directory.Exists(engineDir))
            return;
        DirectoryHelper.DeleteDirectoryRobust(Path.Combine(engineDir, "lm"));
        DirectoryHelper.DeleteDirectoryRobust(Path.Combine(engineDir, "tm"));
        string smtConfigFileName = Path.Combine(engineDir, "smt.cfg");
        if (File.Exists(smtConfigFileName))
            File.Delete(smtConfigFileName);
        if (!Directory.EnumerateFileSystemEntries(engineDir).Any())
            Directory.Delete(engineDir);
    }
}
