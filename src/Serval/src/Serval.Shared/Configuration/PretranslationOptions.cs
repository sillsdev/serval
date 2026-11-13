namespace Serval.Shared.Configuration;

public class PretranslationOptions
{
    public const string Key = "Pretranslation";

    public TimeSpan DeletedPretranslationTimeout { get; set; } = TimeSpan.FromHours(1);
    public string DeletedPretranslationCleanerSchedule { get; set; } = "0 * * * *";
}
