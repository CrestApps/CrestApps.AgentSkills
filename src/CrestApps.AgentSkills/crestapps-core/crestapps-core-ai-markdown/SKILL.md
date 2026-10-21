---
name: crestapps-core-ai-markdown
description: Skill for Markdig-backed Markdown normalization in CrestApps.Core 2.0 AI document, data source and RAG flows. Covers the CrestApps.Core.AI.Markdown package, AddMarkdown on the AI suite builder and AddCoreAIMarkdown on IServiceCollection, MarkdownAITextNormalizer replacing the default IAITextNormalizer, RagTextNormalizer content, chunk and title normalization with HTML stripping and Microsoft.Extensions.DataIngestion.Markdig parsing, the gpt-4o tokenizer chunker with 500 tokens per chunk and 50 tokens of overlap, the plain-text fallback for unsupported inline types, where the normalizer is used (data source indexing, preemptive retrieval, reference titles), and pairing it with document processing. Use it when indexing Markdown-heavy content for retrieval, replacing the text normalizer, or troubleshooting chunk sizes and leftover markup in retrieved text.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Markdown

`CrestApps.Core.AI.Markdown` makes text normalization Markdown-aware. It is opt-in: `AddCoreAIServices()` registers only the plain `DefaultAITextNormalizer`, and the core AI package does not depend on Markdig.

## Guidelines

- Reference the `CrestApps.Core.AI.Markdown` package and call `AddMarkdown()` on `CrestAppsAISuiteBuilder`, or `AddCoreAIMarkdown()` on `IServiceCollection` (namespace `CrestApps.Core.AI.Markdown`).
- Registration adds `MarkdownAITextNormalizer` as the singleton `IAITextNormalizer`. The base registration uses `TryAddSingleton` and this one uses `AddSingleton`, so the Markdown normalizer wins whichever is called first.
- The normalizer delegates to the static `RagTextNormalizer`: HTML is stripped, the text is parsed through `Microsoft.Extensions.DataIngestion.Markdig`, and plain text is extracted before whitespace is normalized.
- `AddMarkdown()` does not register document stores, readers or upload endpoints. Add document processing or ingestion separately (`crestapps-core-document-processing`).

## Register

```csharp
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.OpenXml;
using CrestApps.Core.AI.Documents.Pdf;
using CrestApps.Core.AI.Markdown;
using CrestApps.Core.Data.EntityCore;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddMarkdown()
        .AddDocumentProcessing(documentProcessing => documentProcessing
            .AddEntityCoreStores()
            .AddOpenXml()
            .AddPdf())));
```

Direct registration:

```csharp
builder.Services.AddCoreAIMarkdown();
```

## Behavior

`IAITextNormalizer` has three members, all implemented by `MarkdownAITextNormalizer`:

| Member | Behavior |
|---|---|
| `NormalizeContentAsync(text)` | Strips HTML (line breaks and block closes become newlines, entities are decoded, pilcrows removed), parses Markdown, joins the extracted text, collapses horizontal spaces and repeated blank lines |
| `NormalizeAndChunkAsync(text)` | Strips HTML, parses Markdown, then chunks with `DocumentTokenChunker` using `TiktokenTokenizer.CreateForModel("gpt-4o")`, `MaxTokensPerChunk = 500`, `OverlapTokens = 50` |
| `NormalizeTitle(title)` | Strips HTML and collapses all whitespace to single spaces |

When Markdig reports an unsupported inline type, the normalizer falls back to plain-text normalization and character-based chunking (about 4,000 characters per chunk with 200 characters of overlap) instead of failing the ingestion.

## Where It Is Used

The registered `IAITextNormalizer` is resolved by data source indexing (`DefaultAIDataSourceIndexingService`), data source retrieval and reference titles (`DataSourceRetrieval`), and preemptive retrieval (`DataSourcePreemptiveRagHandler`). Registering Markdown therefore changes both how content is chunked when indexed and how retrieved text and titles look when they reach the model.

## Custom Normalizer

Implement `IAITextNormalizer` (namespace `CrestApps.Core.AI.Services`) to change chunking, and reuse `RagTextNormalizer` for the parts you keep:

```csharp
using CrestApps.Core.AI.Services;

namespace MyApp.Ingestion;

public sealed class HeadingAwareTextNormalizer : IAITextNormalizer
{
    public Task<string> NormalizeContentAsync(string text, CancellationToken cancellationToken = default)
        => RagTextNormalizer.NormalizeContentAsync(text, cancellationToken);

    public async Task<List<string>> NormalizeAndChunkAsync(string text, CancellationToken cancellationToken = default)
    {
        var chunks = await RagTextNormalizer.NormalizeAndChunkAsync(text, cancellationToken);

        return chunks
            .Where(chunk => chunk.Length >= 40)
            .ToList();
    }

    public string NormalizeTitle(string title)
        => RagTextNormalizer.NormalizeTitle(title);
}
```

```csharp
builder.Services.AddSingleton<IAITextNormalizer, HeadingAwareTextNormalizer>();
```

`RagTextNormalizer` ships in the Markdown package, so a custom normalizer that reuses it still needs the package reference. Register the custom normalizer after `AddMarkdown()` so it is the last registration.

## Troubleshooting

- **Retrieved text still contains `**`, `#` or HTML tags**: Markdown normalization is not registered; add `AddMarkdown()`.
- **Chunks are larger or smaller than expected**: the chunker is token-based (gpt-4o tokenizer), not character-based, except in the plain-text fallback.
- **Existing indexes look different after enabling Markdown**: content is chunked at index time; re-index data sources and documents to apply the new normalization.
