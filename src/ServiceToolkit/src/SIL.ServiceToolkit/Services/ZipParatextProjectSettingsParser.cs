namespace SIL.ServiceToolkit.Services;

public class ZipParatextProjectSettingsParser(
    IZipContainer projectContainer,
    ParatextProjectSettings? parentProjectSettings = null
) : ParatextProjectSettingsParserBase(new ZipParatextProjectFileHandler(projectContainer), parentProjectSettings) { }
