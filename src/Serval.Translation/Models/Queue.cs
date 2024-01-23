namespace Serval.Translation.Models;

public class Queue
{
    public int Size { get; set; } = default;
    private string _engineType = default!;
    public string EngineType
    {
        get => _engineType;
        set { _engineType = Engine.ToPascalCase(value); }
    }
}
