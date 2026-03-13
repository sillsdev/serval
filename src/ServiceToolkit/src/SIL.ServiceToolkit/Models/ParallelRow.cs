namespace SIL.ServiceToolkit.Models;

public record ParallelRow(
    string TextId,
    IReadOnlyList<object> SourceRefs,
    IReadOnlyList<object> TargetRefs,
    string SourceSegment,
    string TargetSegment,
    int RowCount
);
