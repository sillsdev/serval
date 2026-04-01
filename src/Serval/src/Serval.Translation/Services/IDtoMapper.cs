namespace Serval.Translation.Services;

public interface IDtoMapper
{
    TranslationEngineDto Map(Engine source);
    TranslationBuildDto Map(Build source);
}
