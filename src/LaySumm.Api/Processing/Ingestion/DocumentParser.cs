using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LaySumm.Api.Processing.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace LaySumm.Api.Processing.Ingestion;

public sealed class DocumentParser
{
    private static readonly Regex ReferenceRegex = new("\\[(?<id>[^\\]]+)\\]", RegexOptions.Compiled);

    private readonly AudioTranscriptionService _audioTranscriptionService;
    private readonly ILogger<DocumentParser> _logger;

    public DocumentParser(AudioTranscriptionService audioTranscriptionService, ILogger<DocumentParser> logger)
    {
        _audioTranscriptionService = audioTranscriptionService;
        _logger = logger;
    }

    public async Task<StructuredDocument> ParseAsync(string documentId, IngestedFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => ParsePdf(documentId, file),
            ".docx" => ParseWord(documentId, file),
            ".doc" => ParseWord(documentId, file),
            ".mp3" or ".wav" or ".m4a" => await ParseAudioAsync(documentId, file, cancellationToken),
            _ => ParseText(documentId, file)
        };
    }

    private StructuredDocument ParsePdf(string documentId, IngestedFile file)
    {
        using var stream = new MemoryStream(file.Content);
        using var document = PdfDocument.Open(stream);

        var sections = new List<DocumentSection>();
        var figures = new List<DocumentFigure>();
        var tables = new List<DocumentTable>();

        foreach (var page in document.GetPages())
        {
            var sectionId = $"{documentId}-page-{page.Number}";
            var sectionSpans = ExtractPdfSpans(sectionId, page).ToList();
            sections.Add(new DocumentSection
            {
                SectionId = sectionId,
                Title = $"Page {page.Number}",
                Spans = sectionSpans,
                References = ExtractReferences(sectionSpans),
                Layout = new SectionLayout
                {
                    PageStart = page.Number,
                    PageEnd = page.Number,
                    BoundingBoxes = sectionSpans
                        .Where(span => span.BoundingBox is not null)
                        .Select(span => span.BoundingBox!)
                        .ToList()
                }
            });
        }

        return new StructuredDocument
        {
            DocumentId = documentId,
            FileName = file.FileName,
            Sections = sections,
            Figures = figures,
            Tables = tables,
            Metadata = new Dictionary<string, string>
            {
                ["PageCount"] = document.NumberOfPages.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private static IEnumerable<DocumentSpan> ExtractPdfSpans(string sectionId, Page page)
    {
        var spans = new List<DocumentSpan>();
        var paragraphs = page.GetText().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var paragraphIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            paragraphIndex++;
            var spanId = $"{sectionId}-para-{paragraphIndex}";
            var words = page.GetWords().Where(w => paragraph.Contains(w.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            BoundingBox? boundingBox = null;

            if (words.Count > 0)
            {
                var minX = words.Min(w => w.BoundingBox.Left);
                var maxX = words.Max(w => w.BoundingBox.Right);
                var minY = words.Min(w => w.BoundingBox.Bottom);
                var maxY = words.Max(w => w.BoundingBox.Top);
                boundingBox = new BoundingBox
                {
                    X = minX,
                    Y = minY,
                    Width = maxX - minX,
                    Height = maxY - minY
                };
            }

            spans.Add(new DocumentSpan
            {
                SpanId = spanId,
                SourceReference = $"page:{page.Number}",
                Text = paragraph.Trim(),
                PageNumber = page.Number,
                BoundingBox = boundingBox
            });
        }

        return spans;
    }

    private StructuredDocument ParseWord(string documentId, IngestedFile file)
    {
        using var stream = new MemoryStream(file.Content);
        using var wordDocument = WordprocessingDocument.Open(stream, false);
        var body = wordDocument.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            throw new InvalidOperationException("Word document has no body content.");
        }

        var sections = new List<DocumentSection>();
        var currentSectionSpans = new List<DocumentSpan>();
        var currentTitle = "Document";
        var sectionIndex = 0;
        var spanIndex = 0;

        foreach (var element in body.Elements<Paragraph>())
        {
            var paragraphText = element.InnerText?.Trim();
            if (string.IsNullOrWhiteSpace(paragraphText))
            {
                continue;
            }

            if (IsHeading(element))
            {
                FlushSection();
                currentTitle = paragraphText;
                continue;
            }

            spanIndex++;
            currentSectionSpans.Add(new DocumentSpan
            {
                SpanId = $"{documentId}-span-{spanIndex}",
                SourceReference = $"paragraph:{spanIndex}",
                Text = paragraphText,
                PageNumber = 0,
                BoundingBox = null
            });
        }

        FlushSection();

        return new StructuredDocument
        {
            DocumentId = documentId,
            FileName = file.FileName,
            Sections = sections,
            Figures = new List<DocumentFigure>(),
            Tables = new List<DocumentTable>(),
            Metadata = new Dictionary<string, string>
            {
                ["WordSectionCount"] = sections.Count.ToString(CultureInfo.InvariantCulture)
            }
        };

        void FlushSection()
        {
            if (currentSectionSpans.Count == 0)
            {
                return;
            }

            sectionIndex++;
            var sectionId = $"{documentId}-section-{sectionIndex}";
            sections.Add(new DocumentSection
            {
                SectionId = sectionId,
                Title = currentTitle,
                Spans = currentSectionSpans.ToList(),
                References = ExtractReferences(currentSectionSpans),
                Layout = new SectionLayout
                {
                    PageStart = 0,
                    PageEnd = 0,
                    BoundingBoxes = new List<BoundingBox>()
                }
            });

            currentSectionSpans = new List<DocumentSpan>();
        }
    }

    private async Task<StructuredDocument> ParseAudioAsync(string documentId, IngestedFile file, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running ASR for audio document {FileName}", file.FileName);
        var transcript = await _audioTranscriptionService.TranscribeAsync(documentId, file, cancellationToken);
        var spans = transcript.Segments
            .Select(segment => new DocumentSpan
            {
                SpanId = segment.SegmentId,
                SourceReference = $"timestamp:{segment.Start:0.00}-{segment.End:0.00}",
                Text = segment.Text,
                PageNumber = 0,
                BoundingBox = null
            })
            .ToList();

        return new StructuredDocument
        {
            DocumentId = documentId,
            FileName = file.FileName,
            Sections = new List<DocumentSection>
            {
                new()
                {
                    SectionId = $"{documentId}-audio", 
                    Title = "Audio Transcript",
                    Spans = spans,
                    References = ExtractReferences(spans),
                    Layout = new SectionLayout
                    {
                        PageStart = 0,
                        PageEnd = 0,
                        BoundingBoxes = new List<BoundingBox>()
                    }
                }
            },
            Figures = new List<DocumentFigure>(),
            Tables = new List<DocumentTable>(),
            Metadata = new Dictionary<string, string>
            {
                ["DurationSeconds"] = transcript.Duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private StructuredDocument ParseText(string documentId, IngestedFile file)
    {
        var text = System.Text.Encoding.UTF8.GetString(file.Content);
        var paragraphs = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var spans = paragraphs.Select((paragraph, index) => new DocumentSpan
        {
            SpanId = $"{documentId}-text-{index}",
            SourceReference = $"line:{index}",
            Text = paragraph.Trim(),
            PageNumber = 0,
            BoundingBox = null
        }).ToList();

        return new StructuredDocument
        {
            DocumentId = documentId,
            FileName = file.FileName,
            Sections = new List<DocumentSection>
            {
                new()
                {
                    SectionId = $"{documentId}-text",
                    Title = "Document",
                    Spans = spans,
                    References = ExtractReferences(spans),
                    Layout = new SectionLayout
                    {
                        PageStart = 0,
                        PageEnd = 0,
                        BoundingBoxes = new List<BoundingBox>()
                    }
                }
            },
            Figures = new List<DocumentFigure>(),
            Tables = new List<DocumentTable>(),
            Metadata = new Dictionary<string, string>()
        };
    }

    private static IReadOnlyList<DocumentReference> ExtractReferences(IEnumerable<DocumentSpan> spans)
    {
        var references = new Dictionary<string, DocumentReference>();
        foreach (var match in spans.SelectMany(span => ReferenceRegex.Matches(span.Text).Cast<Match>()))
        {
            var id = match.Groups["id"].Value;
            if (!references.ContainsKey(id))
            {
                references[id] = new DocumentReference
                {
                    ReferenceId = id,
                    CitationText = match.Value,
                    Doi = null,
                    Url = null
                };
            }
        }

        return references.Values.ToList();
    }

    private static bool IsHeading(Paragraph paragraph)
    {
        var paragraphProperties = paragraph.ParagraphProperties;
        var paragraphStyleId = paragraphProperties?.ParagraphStyleId?.Val?.Value;
        if (paragraphStyleId is null)
        {
            return false;
        }

        return paragraphStyleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase);
    }
}
