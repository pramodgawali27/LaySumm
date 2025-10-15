using System.Collections.Generic;

namespace LaySumm.Api.Processing.Models;

public sealed class StructuredDocument
{
    public required string DocumentId { get; init; }

    public required string FileName { get; init; }

    public required IReadOnlyList<DocumentSection> Sections { get; init; }

    public required IReadOnlyList<DocumentFigure> Figures { get; init; }

    public required IReadOnlyList<DocumentTable> Tables { get; init; }

    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}

public sealed class DocumentSection
{
    public required string SectionId { get; init; }

    public required string Title { get; init; }

    public required IReadOnlyList<DocumentSpan> Spans { get; init; }

    public required IReadOnlyList<DocumentReference> References { get; init; }

    public required SectionLayout Layout { get; init; }
}

public sealed class SectionLayout
{
    public int PageStart { get; init; }

    public int PageEnd { get; init; }

    public required IReadOnlyList<BoundingBox> BoundingBoxes { get; init; }
}

public sealed class BoundingBox
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }
}

public sealed class DocumentSpan
{
    public required string SpanId { get; init; }

    public required string SourceReference { get; init; }

    public required string Text { get; init; }

    public required int PageNumber { get; init; }

    public required BoundingBox? BoundingBox { get; init; }
}

public sealed class DocumentReference
{
    public required string ReferenceId { get; init; }

    public required string CitationText { get; init; }

    public string? Doi { get; init; }

    public string? Url { get; init; }
}

public sealed class DocumentFigure
{
    public required string FigureId { get; init; }

    public required string Caption { get; init; }

    public required IReadOnlyList<BoundingBox> BoundingBoxes { get; init; }

    public required int PageNumber { get; init; }

    public string? BlobUri { get; init; }
}

public sealed class DocumentTable
{
    public required string TableId { get; init; }

    public required string Caption { get; init; }

    public required IReadOnlyList<BoundingBox> BoundingBoxes { get; init; }

    public required int PageNumber { get; init; }

    public string? BlobUri { get; init; }
}
