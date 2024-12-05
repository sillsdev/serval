namespace Serval.Machine.Shared.Configuration;

public class WordAlignmentModelOptions
{
    public const string Key = "WordAlignmentModel";

    public WordAlignmentModelOptions()
    {
        string installDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
        NewModelFile = Path.Combine(installDir, "thot-new-model.zip");
    }

    public string NewModelFile { get; set; }
}
