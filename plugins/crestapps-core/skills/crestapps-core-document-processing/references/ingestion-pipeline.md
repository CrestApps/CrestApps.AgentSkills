# Ingestion Pipeline Reference

Detail for `crestapps-core-document-processing`. Everything here lives in `CrestApps.Core.AI.Ingestion` unless noted, and works the same for chat uploads and for file sources.

## Flow

```text
Stream + file name + media type
  -> IIngestionDocumentReaderResolver   (media type, then magic bytes, then extension)
  -> IngestionDocumentReader            (PlainText, Pdf, OpenXml, Html, DocumentIntelligence, yours)
  -> AIDocumentIngestionProcessor[]     (registration order: captions, salience, description, yours)
  -> IngestionDocument
       chat uploads   -> DefaultAIDocumentProcessingService (chunks, embeddings, DocumentFigureList)
       knowledge base -> IKnowledgeIngestionService (document / article / text / figure / chart / table objects)
```

`IAIDocumentIngestionPipeline.IngestAsync(stream, fileName, mediaType, DocumentIngestionContext, ct)` runs the whole chain and returns the enriched `IngestionDocument`.

## Reader Resolution

`IIngestionDocumentReaderResolver` tries, in order:

1. the declared media type when it is specific,
2. the first bytes (`%PDF-`, PNG, JPEG, or a zip container disambiguated by extension),
3. the extension.

The stream position is restored afterwards. Readers are keyed both by extension and by media type, and the last keyed registration wins.

## A Custom Reader

```csharp
using System.Text;
using CrestApps.Core.AI.Ingestion;
using Microsoft.Extensions.DataIngestion;

public sealed class RtfIngestionDocumentReader : IngestionDocumentReader
{
    public override async Task<IngestionDocument> ReadAsync(
        Stream source,
        string identifier,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rtf = await reader.ReadToEndAsync(cancellationToken);
        var text = RtfText.Strip(rtf); // your RTF-to-text conversion

        var document = new IngestionDocument(identifier);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var section = new IngestionDocumentSection();
            section.Elements.Add(new IngestionDocumentParagraph(text)
            {
                Text = text,
            });

            document.Sections.Add(section);
        }

        return document;
    }
}

builder.Services.AddCoreAIIngestionDocumentReader<RtfIngestionDocumentReader>(".rtf");
```

Set `PageNumber` and element text on every element you emit. Page attribution set only on a section is lost, because content enumeration does not yield sections.

## PDF Layout Analysis (`CrestApps.Core.AI.Ingestion.Pdf`)

Pages are segmented into blocks and ordered by geometry, so columns read top to bottom. Running heads and printed page numbers are kept as header and footer elements marked `ElementMetadataKeys.IsDecoration`; consumers skip them through `element.IsDecoration()`. Ruled and whitespace-aligned tables become `IngestionDocumentTable`, drawn charts become PNG figures, and a chart whose tick labels solve a straight scale gets `ValueConfidence = Exact` values.

```csharp
builder.Services.Configure<PdfLayoutOptions>(options =>
{
    options.UseLayoutAnalysis = true;             // false = one raw paragraph per page (escape hatch)
    options.StripDecoration = true;
    options.EmitDecorationAsHeaderFooter = true;  // false drops decoration instead of marking it
    options.MaxDecorationCharacters = 200;
    options.EmitImages = true;
    options.MinImageSamples = 32;                 // smaller images are rules, bullets or icons
    options.EmitTables = true;
    options.EmitVectorFigures = true;
    options.MaxVectorSegmentsPerPage = 4000;      // denser pages skip drawn-table and figure detection
});
```

Text repair is script-agnostic: hyphenated line breaks are rejoined, ligatures and presentation forms expanded, accents composed with `FormC`, invisible formatting characters dropped (ZWNJ and ZWJ kept), and digits left untouched. `TextSegmentation` handles sentence boundaries for scripts without a full stop or letter case. A page that defeats segmentation keeps its raw text, and a failed analysis pass falls back to the raw reader for the whole document.

## Figures

### Captions (`FigureCaptionProcessor`)

Calls no model. It learns the document's caption direction from unambiguous numbered captions and falls back to `CaptionPatternOptions.DefaultFigureDirection` (Below) and `DefaultTableDirection` (Above). Patterns ship for most European languages plus Japanese, Chinese and Korean. Add a language:

```csharp
using System.Text.RegularExpressions;
using CrestApps.Core.AI.Ingestion.Processors;

builder.Services.Configure<CaptionPatternOptions>(options =>
{
    options.Patterns.Add(new CaptionPattern
    {
        Expression = new Regex(@"^Kuva\s*\d+", RegexOptions.IgnoreCase),
        Bucket = CaptionBuckets.Figure,
    });
});
```

Replace `IFigureCaptionCandidateDetector` or `IFigureCaptionResolver` for an unusual layout. A caption reported by Document Intelligence is marked `provider` and left alone.

### Salience (`FigureSalienceProcessor`)

Scores each figure from cheap signals (no pixel decoding) into `Skip`, `CaptionOnly` or `Describe`. Tune with `FigureSalienceOptions`: `PatternCaptionScore` (3), `TypographyCaptionScore` (2), `CitedInProseScore` (2), `RepeatedArtworkPenalty` (4), `SmallOrExtremePenalty` (2), `FullBleedPenalty` (3), `DescribeThreshold` (3), and the scanned-document rule `ScannedDocumentPageRatio` (0.5), `ScannedPageMaxCharacters` (40), `ScannedPageScore` (3). `DocumentIngestionContext.FigureMode = Off` drops every figure. `MaxFigureDescriptionsPerDocument` demotes the surplus to `CaptionOnly`.

### Transcription (`FigureDescriptionProcessor`)

The only processor that calls a model. It sends each `Describe` figure to the vision deployment through `IImageAnalysisService.AnalyzeAsync(ImageAnalysisRequest, ct)` with the `figure-transcription` prompt, which asks for a literal transcription and forbids inventing values. Results are cached by content hash and `FigureDescriptionProcessor.FigureTranscriptionPromptVersion` through `IFigureDescriptionCache` (in memory by default). Replace the cache to share transcriptions across nodes:

```csharp
using CrestApps.Core.AI.Ingestion.Processors;
using Microsoft.Extensions.Caching.Distributed;

public sealed class DistributedFigureDescriptionCache : IFigureDescriptionCache
{
    private readonly IDistributedCache _cache;

    public DistributedFigureDescriptionCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public Task<string> TryGetAsync(string contentHash, string promptVersion, CancellationToken cancellationToken = default)
        => _cache.GetStringAsync(GetKey(contentHash, promptVersion), cancellationToken);

    public Task SetAsync(string contentHash, string promptVersion, string description, CancellationToken cancellationToken = default)
        => _cache.SetStringAsync(GetKey(contentHash, promptVersion), description, cancellationToken);

    private static string GetKey(string contentHash, string promptVersion)
        => $"figure-description:{promptVersion}:{contentHash}";
}

builder.Services.AddSingleton<IFigureDescriptionCache, DistributedFigureDescriptionCache>();
```

Register it before `AddFigureProcessing()` (or before `AddDocumentProcessing()` / `AddCoreFileSources()`), because the built-in cache is a `TryAdd`.

Failure never fails an ingest. No vision deployment, or one without image input, is logged once and the document continues as text. A throwing call drops that figure to `CaptionOnly`.

In knowledge ingestion, figures are never transcribed inline. They are stored with `KnowledgeObjectStatus.PendingDescription`, stay searchable by caption, and are finished by `IFigureDescriptionBackfillService` (`BackfillDueAsync`, `BackfillAsync(dataSource)`), driven by the hosted job from `AddFigureBackfill()` every `KnowledgeIngestionOptions.BackfillIntervalSeconds` (30) with `BackfillBatchSize` (20) and `MaxConcurrentVisionCalls` (2).

### How a figure reaches chunk text

```text
[figure report.pdf-p8-1 | page 8 | Figure 3. Measured values against the model.]
{the transcription}
[/figure]
```

The fenced block keeps a description with its figure across chunk boundaries. Figure bytes are written through `IDocumentFileStore` and listed on the `AIDocument` as a `DocumentFigureList` (`document.GetFigures()`, `document.FindFigure(id)`).

## Azure AI Document Intelligence

```csharp
using CrestApps.Core.AI.Ingestion.Pdf;
using CrestApps.Core.Azure.DocumentIntelligence;

builder.Services.AddCoreAIDocumentIngestion(ingestion => ingestion
    .AddPlainTextReader()
    .AddFigureProcessing()
    .AddFigureBackfill()
    .AddPdf()
    .AddDocumentIntelligence(options =>
    {
        options.Endpoint = builder.Configuration["DocumentIntelligence:Endpoint"];
        options.ApiKey = builder.Configuration["DocumentIntelligence:ApiKey"]; // omit to use DefaultAzureCredential
        options.MaxPages = 200;
    }));
```

- `DocumentIntelligenceOptions`: `Endpoint`, `ApiKey`, `ModelId` (`prebuilt-layout`), `Extensions` (`.pdf`), `IncludeFigureContent` (true), `MaxPages`, `Timeout` (5 minutes).
- The configure callback runs at registration time. When `Endpoint` is empty nothing is registered and the local readers keep serving.
- Register it after `AddPdf()` so its keyed reader wins. It is wrapped in `FallbackIngestionDocumentReader`, so throttling, page limits, timeouts or outages fall back to the local reader instead of rejecting the document.
- It reads scanned pages, which the local readers cannot.

## Knowledge Ingestion From Code

```csharp
using CrestApps.Core.AI.Ingestion.Knowledge;

var result = await knowledgeIngestionService.IngestAsync(
    dataSource,                 // an AIDataSource whose Source is AIDataSourceSourceTypes.File
    stream,
    fileName,
    mediaType,
    new KnowledgeIngestionOptions
    {
        FigureMode = FigureProcessingMode.Auto,
        Language = "de",
    },
    cancellationToken);

// result.RootId, ObjectCount, FigureCount, PendingDescriptionCount, Success, Error
await knowledgeIngestionService.RemoveAsync(dataSource, result.RootId, cancellationToken);
```

The root identifier comes from the bytes. Re-ingesting identical bytes replaces what they produced before, including index rows and stored figures that the new ingest no longer produces. `KnowledgeIngestionOptions.ChartKeywords` replaces the default chart keywords for other languages.

Structure analysis (`IDocumentStructureAnalyzer` with registered `IDocumentStructureStrategy` rungs) splits a publication into articles from its outline, stated headings, table of contents or inferred headings, and degrades to one article when unsure. `IPublicationMetadataExtractor` spends one utility-model call on front-matter metadata.
