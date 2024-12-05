namespace Serval.Machine.Shared.Services;

public class WordAlignmentModelFactory(IOptionsMonitor<WordAlignmentModelOptions> options)
    : ModelFactoryBase,
        IWordAlignmentModelFactory
{
    private readonly IOptionsMonitor<WordAlignmentModelOptions> _options = options;

    public IWordAlignmentModel Create(string engineDir)
    {
        var modelPath = Path.Combine(engineDir, "tm", "src_trg");
        var directModel = ThotWordAlignmentModel.Create(ThotWordAlignmentModelType.Hmm);
        directModel.Load(modelPath + "_invswm");

        var inverseModel = ThotWordAlignmentModel.Create(ThotWordAlignmentModelType.Hmm);
        inverseModel.Load(modelPath + "_swm");

        return new SymmetrizedWordAlignmentModel(directModel, inverseModel);
    }

    public ITrainer CreateTrainer(
        string engineDir,
        ITokenizer<string, int, string> tokenizer,
        IParallelTextCorpus corpus
    )
    {
        var modelPath = Path.Combine(engineDir, "tm", "src_trg");
        var directModel = ThotWordAlignmentModel.Create(ThotWordAlignmentModelType.Hmm);
        directModel.Load(modelPath + "_invswm");

        var inverseModel = ThotWordAlignmentModel.Create(ThotWordAlignmentModelType.Hmm);
        inverseModel.Load(modelPath + "_swm");

        ITrainer directTrainer = directModel.CreateTrainer(corpus, tokenizer);
        ITrainer inverseTrainer = inverseModel.CreateTrainer(corpus.Invert(), tokenizer);

        return new SymmetrizedWordAlignmentModelTrainer(directTrainer, inverseTrainer);
    }

    public override void InitNew(string engineDir)
    {
        if (!Directory.Exists(engineDir))
            Directory.CreateDirectory(engineDir);
        ZipFile.ExtractToDirectory(_options.CurrentValue.NewModelFile, engineDir);
    }
}
