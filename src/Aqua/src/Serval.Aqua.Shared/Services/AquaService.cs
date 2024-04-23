namespace Serval.Aqua.Shared.Services;

public class AquaService(IHttpClientFactory httpClientFactory, ILanguageTagService languageTagService) : IAquaService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) }
        };

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Aqua");
    private readonly ILanguageTagService _languageTagService = languageTagService;

    public async Task<VersionDto> CreateVersionAsync(
        string name,
        string language,
        string abbreviation,
        CancellationToken cancellationToken = default
    )
    {
        if (!_languageTagService.TryParse(language, out string? languageCode, out string? scriptCode))
            throw new InvalidOperationException($"Invalid language tag '{language}'.");

        var content = JsonContent.Create(
            new
            {
                name,
                iso_language = languageCode,
                iso_script = scriptCode,
                abbreviation
            },
            options: JsonSerializerOptions
        );

        return await CallAsync<VersionDto>(
            HttpMethod.Post,
            "version",
            content: content,
            cancellationToken: cancellationToken
        );
    }

    public async Task DeleteVersionAsync(int versionId, CancellationToken cancellationToken = default)
    {
        Dictionary<string, string?> parameters = new() { ["id"] = versionId.ToString(CultureInfo.InvariantCulture) };
        await CallAsync(HttpMethod.Delete, "version", parameters, cancellationToken: cancellationToken);
    }

    public async Task<RevisionDto> CreateRevisionAsync(
        int versionId,
        string fileName,
        CancellationToken cancellationToken = default
    )
    {
        Dictionary<string, string?> parameters =
            new() { ["version_id"] = versionId.ToString(CultureInfo.InvariantCulture) };
        MultipartFormDataContent content = [];
        StreamContent fileContent = new(File.OpenRead(fileName));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        content.Add(fileContent, "file", Path.GetFileName(fileName));
        return await CallAsync<RevisionDto>(HttpMethod.Post, "revision", parameters, content, cancellationToken);
    }

    public async Task DeleteRevisionAsync(int revisionId, CancellationToken cancellationToken = default)
    {
        Dictionary<string, string?> parameters = new() { ["id"] = revisionId.ToString(CultureInfo.InvariantCulture) };
        await CallAsync(HttpMethod.Delete, "revision", parameters, cancellationToken: cancellationToken);
    }

    public async Task<AssessmentDto> CreateAssessmentAsync(
        AssessmentType type,
        int revisionId,
        int? referenceId = null,
        CancellationToken cancellationToken = default
    )
    {
        Dictionary<string, string?> parameters =
            new()
            {
                ["revision_id"] = revisionId.ToString(CultureInfo.InvariantCulture),
                ["type"] = type.ToString().ToKebabCase(),
                ["modal_suffix"] = "_aws"
            };
        if (referenceId is not null)
            parameters["reference_id"] = referenceId.Value.ToString(CultureInfo.InvariantCulture);
        IReadOnlyList<AssessmentDto> assessments = await CallAsync<IReadOnlyList<AssessmentDto>>(
            HttpMethod.Post,
            "assessment",
            parameters,
            cancellationToken: cancellationToken
        );
        return assessments.Single();
    }

    public async Task DeleteAssessmentAsync(int assessmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            Dictionary<string, string?> parameters =
                new() { ["assessment_id"] = assessmentId.ToString(CultureInfo.InvariantCulture) };
            await CallAsync(HttpMethod.Delete, "assessment", parameters, cancellationToken: cancellationToken);
        }
        catch
        {
            // TODO: we currently get a 403 when deleting assessments
        }
    }

    public async Task<IReadOnlyList<AssessmentDto>> GetAssessmentsAsync(CancellationToken cancellationToken = default)
    {
        return await CallAsync<List<AssessmentDto>>(HttpMethod.Get, "assessment", cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ResultDto>> GetResultsAsync(
        int assessmentId,
        string? book = null,
        int? chapter = null,
        CancellationToken cancellationToken = default
    )
    {
        Dictionary<string, string?> parameters =
            new() { ["assessment_id"] = assessmentId.ToString(CultureInfo.InvariantCulture) };
        if (book is not null)
            parameters["book"] = book;
        if (chapter is not null)
            parameters["chapter"] = chapter.Value.ToString(CultureInfo.InvariantCulture);
        ResultsDto results = await CallAsync<ResultsDto>(
            HttpMethod.Get,
            "result",
            parameters,
            cancellationToken: cancellationToken
        );
        return results.Results;
    }

    private async Task<T> CallAsync<T>(
        HttpMethod method,
        string url,
        IEnumerable<KeyValuePair<string, string?>>? queryParams = null,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
    {
        HttpResponseMessage response = await SendAsync(method, url, queryParams, content, cancellationToken);
        T? result = await response.Content.ReadFromJsonAsync<T>(JsonSerializerOptions, cancellationToken);
        if (result is null)
            throw new InvalidOperationException("The AQuA server returned an invalid response.");
        return result;
    }

    private async Task CallAsync(
        HttpMethod method,
        string url,
        IEnumerable<KeyValuePair<string, string?>>? queryParams = null,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
    {
        await SendAsync(method, url, queryParams, content, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        IEnumerable<KeyValuePair<string, string?>>? queryParams = null,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
    {
        if (queryParams is not null)
        {
            var queryString = QueryString.Create(queryParams);
            url += queryString;
        }
        HttpRequestMessage request = new(method, url) { Content = content };
        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
