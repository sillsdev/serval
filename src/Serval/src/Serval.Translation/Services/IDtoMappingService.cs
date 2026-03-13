namespace Serval.Translation.Services;

public interface IDtoMappingService
{
    TranslationEngineDto Map(Engine source);
    TranslationBuildDto Map(Build source);
}
