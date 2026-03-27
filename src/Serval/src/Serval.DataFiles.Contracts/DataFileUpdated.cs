namespace Serval.DataFiles.Contracts;

public record DataFileUpdated(string DataFileId, string Filename) : IEvent;
