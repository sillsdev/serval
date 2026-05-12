namespace Serval.DataFiles.Features.DataFiles;

public record CreateDataFile(string Owner, string? Name, string FileName, FileFormat Format, Stream FileStream)
    : IRequest<CreateDataFileResponse>;

public record CreateDataFileResponse(DataFileDto DataFile);

public class CreateDataFileHandler(
    IRepository<DataFile> dataFiles,
    IIdGenerator idGenerator,
    IOptionsMonitor<DataFileOptions> options,
    IFileSystem fileSystem,
    DtoMapper mapper
) : IRequestHandler<CreateDataFile, CreateDataFileResponse>
{
    public async Task<CreateDataFileResponse> HandleAsync(CreateDataFile request, CancellationToken cancellationToken)
    {
        string filename = Path.GetRandomFileName();
        string path = Path.Combine(options.CurrentValue.FilesDirectory, filename);
        DataFile dataFile = new()
        {
            Id = idGenerator.GenerateId(),
            Name = request.Name ?? request.FileName,
            Format = request.Format,
            Owner = request.Owner,
            Filename = filename,
        };
        try
        {
            await using Stream fileStream = fileSystem.OpenWrite(path);
            await request.FileStream.CopyToAsync(fileStream, cancellationToken);
            await dataFiles.InsertAsync(dataFile, cancellationToken);
        }
        catch
        {
            fileSystem.DeleteFile(path);
            throw;
        }
        return new(mapper.Map(dataFile));
    }
}

public partial class DataFilesController
{
    /// <summary>
    /// Upload a new file
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /files
    ///     {
    ///        "format": "text",
    ///        "name": "myTeam:myProject:myFile.txt"
    ///     }
    ///
    /// </remarks>
    /// <param name="file">
    /// The file to upload.  Max size: 100MB
    /// </param>
    /// <param name="name">
    /// A name to help identify and distinguish the file.
    /// Recommendation: Create a multi-part name to distinguish between projects, uses, languages, etc.
    /// The name does not have to be unique.
    /// Example: myTranslationTeam:myProject:myLanguage:myFile.txt
    /// </param>
    /// <param name="format">
    /// File format options:
    /// * **Text**: One translation unit (a.k.a., verse) per line
    ///   * If a line contains a tab, characters before the tab are used as a unique identifier for the line, characters after the tab are understood as the content of the verse, and if there is another tab following the verse content, characters after this second tab are assumed to be column codes like "ss" etc. for sectioning and other formatting. See this example of a tab-delimited text file:
    ///     > verse_001_005 (tab) Ὑπομνῆσαι δὲ ὑμᾶς βούλομαι , εἰδότας ὑμᾶς ἅπαξ τοῦτο
    ///     > verse_001_006 (tab) Ἀγγέλους τε τοὺς μὴ τηρήσαντας τὴν ἑαυτῶν ἀρχήν , ἀλλὰ (tab) ss
    ///     > verse_001_007 (tab) Ὡς Σόδομα καὶ Γόμορρα , καὶ αἱ περὶ αὐτὰς πόλεις (tab) ss
    ///   * Otherwise, *no tabs* should be used in the file. A unique identifier will be generated for each translation unit based on the line number.
    /// * **Paratext**: A complete, zipped Paratext project backup: that is, a .zip archive of files including the USFM files and "Settings.xml" file. To generate a zipped backup for a project in Paratext, navigate to "Paratext/Advanced/Backup project to file..." and follow the dialogue.
    ///   * USFM files in Paratext projects have unique identifiers assigned per segment for scripture and non-scripture content according to [this guide](https://github.com/sillsdev/serval/wiki/USFM-Parsing-and-Translation)
    /// </param>
    /// <param name="handler"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The file was created successfully</response>
    /// <response code="400">Bad request. Is the file over 100 MB?</response>
    /// <response code="401">The client is not authenticated</response>
    /// <response code="403">The authenticated client cannot perform the operation</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details. </response>
    [Authorize(Scopes.CreateFiles)]
    [HttpPost]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DataFileDto>> CreateAsync(
        [BindRequired] IFormFile file,
        [BindRequired, FromForm] FileFormat format,
        [FromForm] string? name,
        [FromServices] IRequestHandler<CreateDataFile, CreateDataFileResponse> handler,
        CancellationToken cancellationToken
    )
    {
        using Stream stream = file.OpenReadStream();
        CreateDataFileResponse response = await handler.HandleAsync(
            new(Owner, name, file.FileName, format, stream),
            cancellationToken
        );
        return Created(response.DataFile.Url, response.DataFile);
    }
}
