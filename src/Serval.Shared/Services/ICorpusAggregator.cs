namespace Serval.Shared.Services;

public interface ICorpusAggregator
{
    Task InsertCorpusAsync(string id, string owner, CorpusType type, FileFormat format);
    Task DeleteCorpusAsync(string id);
    Task AddDataFileAsync(string id, string fileId, string textId, string languageTag, string filename);
    Task DeleteDataFileAsync(string id, string fileId);
}
