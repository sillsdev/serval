using System.Diagnostics.CodeAnalysis;

namespace Serval.DataFiles.Contracts;

public record GetDataFileResponse(
    [property: MemberNotNullWhen(true, nameof(File))] bool IsFound,
    DataFileContract? File = null
);
