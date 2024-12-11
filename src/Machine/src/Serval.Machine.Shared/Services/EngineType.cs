namespace Serval.Machine.Shared.Services;

public enum EngineType
{
    SmtTransfer,
    Nmt,
    Statistical
}

public enum EngineGroup
{
    Translation,
    WordAlignment
}

public static class EngineTypeMethods
{
    public static EngineGroup ToEngineGroup(EngineType engineType)
    {
        return engineType switch
        {
            EngineType.SmtTransfer => EngineGroup.Translation,
            EngineType.Nmt => EngineGroup.Translation,
            EngineType.Statistical => EngineGroup.WordAlignment,
            _ => throw new ArgumentOutOfRangeException(nameof(engineType), engineType, null)
        };
    }
}
