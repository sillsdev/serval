namespace SIL.ServiceToolkit.Models;

public record Row(
    string TextId,
    IReadOnlyList<object> SourceRefs,
    IReadOnlyList<object> TargetRefs,
    string SourceSegment,
    string TargetSegment,
    int RowCount
);
