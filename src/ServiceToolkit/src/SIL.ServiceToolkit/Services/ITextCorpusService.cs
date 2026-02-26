namespace SIL.ServiceToolkit.Services;

public interface ITextCorpusService
{
    IEnumerable<ITextCorpus> CreateTextCorpora(
        IReadOnlyList<CorpusFile> files,
        IReadOnlyList<CorpusFile> referenceFiles
    );
    IEnumerable<ITextCorpus> CreateTermCorpora(IReadOnlyList<CorpusFile> files);
}
