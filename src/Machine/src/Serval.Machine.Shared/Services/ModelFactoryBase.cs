namespace Serval.Machine.Shared.Services;

public abstract class ModelFactoryBase : IModelFactory
{
    public virtual ITrainer CreateTrainer(
        string engineDir,
        IRangeTokenizer<string, int, string> tokenizer,
        IParallelTextCorpus corpus
    )
    {
        throw new NotImplementedException();
    }

    public virtual void InitNew(string engineDir)
    {
        throw new NotImplementedException();
    }

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

    public async Task SaveEngineToAsync(
        string engineDir,
        Stream destination,
        CancellationToken cancellationToken = default
    )
    {
        // create zip archive in memory stream
        // This cannot be created directly to the shared stream because it all needs to be written at once
        await using MemoryStream memoryStream = new();
        await TarFile.CreateFromDirectoryAsync(
            engineDir,
            memoryStream,
            includeBaseDirectory: false,
            cancellationToken: cancellationToken
        );
        memoryStream.Seek(0, SeekOrigin.Begin);
        await using GZipStream gzipStream = new(destination, CompressionMode.Compress);
        await memoryStream.CopyToAsync(gzipStream, cancellationToken);
    }
}
