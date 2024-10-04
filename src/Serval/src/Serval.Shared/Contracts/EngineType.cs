namespace Serval.Shared.Contracts;

public enum EngineType
{
    Translation,
    Assessment,
    WordAlignment
}

public class EngineTypeResolver
{
    public static EngineType GetEngineType(string engineType)
    {
        return engineType switch
        {
            "SmtTransfer" => EngineType.Translation,
            "Nmt" => EngineType.Translation,
            "Assessment" => EngineType.Assessment,
            "WordAlignment" => EngineType.WordAlignment,
            _ => throw new ArgumentException($"Unknown engine type: {engineType}")
        };
    }
}
