namespace Serval.Shared.Services;

public class ZipParatextProjectSettingsParser(
    IZipContainer projectContainer,
    IZipContainer? parentProjectContainer = null
)
    : ParatextProjectSettingsParserBase(
        new ZipParatextProjectFileHandler(projectContainer),
        parentProjectContainer != null ? new ZipParatextProjectSettingsParser(parentProjectContainer).Parse() : null
    ) { }
