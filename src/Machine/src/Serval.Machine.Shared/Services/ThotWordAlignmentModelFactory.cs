namespace Serval.Machine.Shared.Services;

public class ThotWordAlignmentModelFactory(IOptionsMonitor<ThotWordAlignmentModelOptions> options)
    : ModelFactoryBase,
        IWordAlignmentModelFactory
{
    private readonly IOptionsMonitor<ThotWordAlignmentModelOptions> _options = options;

    public IWordAlignmentModel Create(string engineDir, string? modelTypeStr = null)
    {
        var modelPath = Path.Combine(engineDir, "src_trg");
        ThotWordAlignmentModelType modelType = GetEngineModelType(modelPath, modelTypeStr);

        var directModel = ThotWordAlignmentModel.Create(modelType);
        directModel.Load(modelPath + "_invswm");

        var inverseModel = ThotWordAlignmentModel.Create(modelType);
        inverseModel.Load(modelPath + "_swm");

        return new SymmetrizedWordAlignmentModel(directModel, inverseModel);
    }

    public ITrainer CreateTrainer(
        string engineDir,
        ITokenizer<string, int, string> tokenizer,
        IParallelTextCorpus corpus,
        string? modelTypeStr = null
    )
    {
        var modelPath = Path.Combine(engineDir, "src_trg");
        ThotWordAlignmentModelType modelType = GetEngineModelType(modelPath, modelTypeStr);

        var directModel = ThotWordAlignmentModel.Create(modelType);
        directModel.SourceTokenizer = tokenizer;
        directModel.TargetTokenizer = tokenizer;
        directModel.Load(modelPath + "_invswm");

        var inverseModel = ThotWordAlignmentModel.Create(modelType);
        inverseModel.SourceTokenizer = tokenizer;
        inverseModel.TargetTokenizer = tokenizer;
        inverseModel.Load(modelPath + "_swm");

        ITrainer directTrainer = directModel.CreateTrainer(corpus);
        ITrainer inverseTrainer = inverseModel.CreateTrainer(corpus.Invert());

        return new SymmetrizedWordAlignmentModelTrainer(directTrainer, inverseTrainer);
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
        string? modelPath = Path.Combine(engineDir, "src_trg");
        string configFileName = modelPath + "_invswm.yml";
        if (File.Exists(configFileName))
            File.Delete(configFileName);

        DirectoryHelper.DeleteDirectoryRobust(modelPath);

        if (!Directory.EnumerateFileSystemEntries(engineDir).Any())
            Directory.Delete(engineDir);
    }

    private static ThotWordAlignmentModelType GetEngineModelType(string modelPath, string? modelTypeStr = null)
    {
        ThotWordAlignmentModelType modelType = ThotWordAlignmentModelType.Hmm;

        string configPath = modelPath + "_invswm.yml";

        if (modelTypeStr is not null)
        {
            modelType = ThotWordAlignmentModelTypeHelpers.GetThotWordAlignmentModelType(modelTypeStr);
        }
        else if (File.Exists(configPath))
        {
            using (var reader = new StreamReader(configPath))
            {
                YamlStream yaml = new();
                yaml.Load(reader);
                var root = (YamlMappingNode)yaml.Documents.First().RootNode;
                modelTypeStr = (string?)root[new YamlScalarNode("model")];
                if (modelTypeStr != null)
                    modelType = ThotWordAlignmentModelTypeHelpers.GetThotWordAlignmentModelType(modelTypeStr);
            }
        }
        return modelType;
    }
}
