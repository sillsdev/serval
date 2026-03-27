namespace Serval.DataFiles.Contracts;

public record GetDataFile(string DataFileId, string Owner) : IRequest<GetDataFileResponse>;
