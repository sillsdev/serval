namespace Serval.Machine.Shared.Services;

public abstract class ModelFactoryBase : IModelFactory
{
    public abstract ITrainer CreateTrainer(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IParallelTextCorpus corpus
    );

    public abstract void InitNew(string engineDir);

    public abstract void Cleanup(string engineDir);

    public async Task UpdateEngineFromAsync(
        string engineDir,
        Stream source,
        CancellationToken cancellationToken = default
    )
    {
        if (!Directory.Exists(engineDir))
            Directory.CreateDirectory(engineDir);

        await using MemoryStream memoryStream = new();
        await using (GZipStream gzipStream = new(source, CompressionMode.Decompress))
        {
            await gzipStream.CopyToAsync(memoryStream, cancellationToken);
        }
        memoryStream.Seek(0, SeekOrigin.Begin);
        await TarFile.ExtractToDirectoryAsync(
            memoryStream,
            engineDir,
            overwriteFiles: true,
            cancellationToken: cancellationToken
        );
    }
}
