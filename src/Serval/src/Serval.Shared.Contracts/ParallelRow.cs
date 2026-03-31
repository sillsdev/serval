namespace Serval.Shared.Contracts;

public record ParallelRow(
    string TextId,
    IReadOnlyList<object> SourceRefs,
    IReadOnlyList<object> TargetRefs,
    string SourceSegment,
    string TargetSegment,
    int RowCount
);
