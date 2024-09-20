namespace Serval.Shared.Services;

public interface IScriptureDataFileService
{
    ParatextProjectSettings GetParatextProjectSettings(string filename);
    ZipParatextProjectTextUpdater GetZipParatextProjectTextUpdater(string filename);
    Dictionary<string, List<int>> GetChapters(IEnumerable<Engine.V1.CorpusFile> corpusFiles, string scriptureRange)
    {
        try
        {
            return ScriptureRangeParser.GetChapters(
                scriptureRange,
                GetParatextProjectSettings(corpusFiles.First().Location).Versification
            );
        }
        catch (ArgumentException ae)
        {
            throw new InvalidOperationException($"The scripture range {scriptureRange} is not valid: {ae.Message}");
        }
    }
}
