namespace Serval.Machine.Shared.Configuration;

public class ThotWordAlignmentModelOptions
{
    public const string Key = "WordAlignmentModel";

    public ThotWordAlignmentModelOptions()
    {
        string installDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
        NewModelFile = Path.Combine(installDir, "thot-new-model.zip");
    }

    public string NewModelFile { get; set; }
}
