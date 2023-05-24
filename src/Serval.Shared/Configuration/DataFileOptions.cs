namespace Serval.Shared.Configuration;

public class DataFileOptions
{
    public const string Key = "DataFile";

    public string FilesDirectory { get; set; } = "";
    public TimeSpan DeletedFileTimeout { get; set; } = TimeSpan.FromHours(24);
    public string DeletedFileCleanerSchedule { get; set; } = "0 0 * * *";
}
