namespace Serval.Machine.Shared.Configuration;

public class ThotSmtModelOptions
{
    public const string Key = "ThotSmtModel";

    public ThotSmtModelOptions()
    {
        string installDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
        NewModelFile = Path.Combine(installDir, "thot-new-model.zip");
    }

    public string NewModelFile { get; set; }
}
