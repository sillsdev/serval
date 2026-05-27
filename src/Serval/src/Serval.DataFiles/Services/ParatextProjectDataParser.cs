namespace Serval.DataFiles.Services;

public class ParatextProjectDataParser
{
    public static async Task<ParatextMetadata> ParseParatextMetadataAsync(string path)
    {
        using ZipContainer zipContainer = new(path);
        try
        {
            ParatextProjectSettings projectSettings = new Shared.Services.ZipParatextProjectSettingsParser(
                zipContainer
            ).Parse();
            return new ParatextMetadata
            {
                ProjectGuid = projectSettings.Guid,
                Name = projectSettings.Name,
                FullName = projectSettings.FullName,
                Versification = projectSettings.Versification.Name,
                TranslationType = projectSettings.TranslationType,
                LanguageCode = projectSettings.LanguageCode,
                Visibility = projectSettings.Visibility,
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Unable to parse the Paratext project settings for the uploaded data file.",
                e
            );
        }
    }
}
