namespace Serval.Translation.Models;

public enum BaseModel
{
    NLLB,
}

public static class BaseModelExtensions
{
    public static string ToModelName(this BaseModel model)
    {
        return model switch
        {
            BaseModel.NLLB => "facebook/nllb-200-distilled-1.3B",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, null),
        };
    }
}
