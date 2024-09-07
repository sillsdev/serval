namespace Serval.Machine.Shared.Services;

public class UnigramTruecaserFactory : ITruecaserFactory
{
    public ITruecaser Create(string engineDir)
    {
        var truecaser = new UnigramTruecaser();
        string path = GetModelPath(engineDir);
        truecaser.Load(path);
        return truecaser;
    }

    public ITrainer CreateTrainer(string engineDir, ITokenizer<string, int, string> tokenizer, ITextCorpus corpus)
    {
        string path = GetModelPath(engineDir);
        ITrainer trainer = new UnigramTruecaserTrainer(path, corpus) { Tokenizer = tokenizer };
        return trainer;
    }

    public void Cleanup(string engineDir)
    {
        string path = GetModelPath(engineDir);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string GetModelPath(string engineDir)
    {
        return Path.Combine(engineDir, "unigram-casing-model.txt");
    }
}
