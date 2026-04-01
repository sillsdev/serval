namespace Serval.Shared.Contracts;

public record ParallelRowContract(
    string TextId,
    IReadOnlyList<object> SourceRefs,
    IReadOnlyList<object> TargetRefs,
    string SourceSegment,
    string TargetSegment,
    int RowCount
);
