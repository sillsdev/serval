namespace SIL.ServiceToolkit.Services;

public interface ITextCorpusService
{
    IEnumerable<ITextCorpus> CreateTextCorpora(IReadOnlyList<CorpusFile> files);
    IEnumerable<ITextCorpus> CreateTermCorpora(IReadOnlyList<CorpusFile> corpusFiles);
}
