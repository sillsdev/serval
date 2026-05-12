namespace Serval.DataFiles.Services;

public class DtoMapper(IUrlService urlService)
{
    public DataFileDto Map(DataFile source) =>
        new()
        {
            Id = source.Id,
            Url = urlService.GetUrl(Endpoints.GetDataFile, new { id = source.Id }),
            Name = source.Name,
            Format = source.Format,
            Revision = source.Revision,
        };

    public CorpusDto Map(Corpus source) =>
        new()
        {
            Id = source.Id,
            Language = source.Language,
            Url = urlService.GetUrl(Endpoints.GetCorpus, new { id = source.Id }),
            Name = source.Name,
            Revision = source.Revision,
            Files = source.Files.Select(Map).ToList(),
        };

    public CorpusFileDto Map(CorpusFile source) =>
        new()
        {
            File = new ResourceLinkDto
            {
                Id = source.FileRef,
                Url = urlService.GetUrl(Endpoints.GetDataFile, new { id = source.FileRef }),
            },
            TextId = source.TextId,
        };
}
