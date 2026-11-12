namespace Serval.Machine.Shared.Services;

public enum EngineType
{
    SmtTransfer,
    Nmt,
    Statistical,
    Echo,
    EchoWordAlignment,
    LlmRemote,
}

public enum EngineGroup
{
    Translation,
    WordAlignment,
}

public static class EngineTypeExtensions
{
    public static EngineGroup ToEngineGroup(this EngineType engineType) =>
        engineType switch
        {
            EngineType.SmtTransfer or EngineType.Nmt or EngineType.Echo or EngineType.LlmRemote =>
                EngineGroup.Translation,
            EngineType.Statistical or EngineType.EchoWordAlignment => EngineGroup.WordAlignment,
            _ => throw new InvalidEnumArgumentException(nameof(engineType), (int)engineType, typeof(EngineType)),
        };
}
