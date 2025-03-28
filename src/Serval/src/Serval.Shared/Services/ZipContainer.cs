﻿using System.IO.Compression;

namespace Serval.Shared.Services;

public class ZipContainer(string fileName) : DisposableBase, IZipContainer
{
    private readonly ZipArchive _archive = ZipFile.OpenRead(fileName);

    public IEnumerable<string> Entries => _archive.Entries.Select(e => e.FullName);

    public bool EntryExists(string name)
    {
        return _archive.GetEntry(name) is not null;
    }

    public Stream OpenEntry(string name)
    {
        ZipArchiveEntry? entry = _archive.GetEntry(name);
        if (entry is null)
            throw new ArgumentException("The specified entry does not exist.", nameof(name));
        return entry.Open();
    }

    protected override void DisposeManagedResources()
    {
        _archive.Dispose();
    }
}
