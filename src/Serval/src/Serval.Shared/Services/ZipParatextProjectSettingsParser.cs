namespace Serval.Shared.Services;

public class ZipParatextProjectSettingsParser(IZipContainer projectContainer)
    : ParatextProjectSettingsParserBase(new ZipParatextProjectFileHandler(projectContainer)) { }
