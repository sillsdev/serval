namespace Serval.Shared.Services;

public interface IZipContainer : IDisposable
{
    bool EntryExists(string name);
    Stream OpenEntry(string name);
    IEnumerable<string> Entries { get; }
}
