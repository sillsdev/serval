namespace Serval.DataFiles.Contracts;

public record DataFileDeleted(string DataFileId) : IEvent;
