namespace SIL.ServiceToolkit.Models;

public record Row(string TextId, IReadOnlyList<object> Refs, string SourceSegment, string TargetSegment, int RowCount);
