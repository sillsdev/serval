namespace Serval.Translation.Models;

public class LanguageInfo
{
    private string _engineType = default!;
    public string EngineType
    {
        get => _engineType;
        set { _engineType = Engine.ToPascalCase(value); }
    }
    public bool IsNative { get; set; } = default!;
    public string? InternalCode { get; set; } = default!;
    public string? Name { get; set; } = default!;
}
