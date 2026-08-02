---
name: crestapps-core-ai-markdown
description: Skill for registering Markdig-backed Markdown normalization in CrestApps.Core AI document and RAG flows.
---

# CrestApps.Core AI Markdown - Prompt Templates

## Normalize Markdown for AI

You are a CrestApps.Core expert. Generate code and guidance for Markdown-aware text normalization used by CrestApps.Core document-processing and RAG workflows.

### Guidelines

- Reference the standalone `CrestApps.Core.AI.Markdown` package when Markdown-aware normalization is needed.
- Call `AddMarkdown()` on `CrestAppsAISuiteBuilder`, or call `AddCoreAIMarkdown()` directly on `IServiceCollection`.
- Registration adds `MarkdownAITextNormalizer` as the singleton `IAITextNormalizer`.
- The normalizer delegates content, chunking, and title cleanup to `RagTextNormalizer`.
- It strips HTML, parses Markdown through `Microsoft.Extensions.DataIngestion.Markdig`, and extracts plain text before normalizing whitespace.

### Register Markdown Support

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddMarkdown()
        .AddDocumentProcessing(documentProcessing => documentProcessing
            .AddEntityCoreStores()
            .AddOpenXml()
            .AddPdf())));
```

For direct service registration:

```csharp
builder.Services.AddCoreAIMarkdown();
```

Register Markdown after the base AI services when replacing their default `IAITextNormalizer`.

### Normalization Behavior

| Operation | `MarkdownAITextNormalizer` method | Behavior |
|---|---|---|
| Document content | `NormalizeContentAsync` | HTML stripping, Markdown parsing, text extraction, whitespace cleanup |
| Retrieval chunks | `NormalizeAndChunkAsync` | Markdown parsing followed by token-aware chunking |
| Titles | `NormalizeTitle` | HTML stripping and whitespace cleanup |

The default chunker uses the `gpt-4o` tokenizer with 500 tokens per chunk and 50 overlapping tokens. If Markdig encounters an unsupported inline type, the normalizer uses its built-in plain-text fallback instead of failing the ingestion flow.

### Use with Document Processing

`AddMarkdown()` complements `AddDocumentProcessing(...)`; it does not register document stores, upload endpoints, PDF readers, or Office readers. Add those separately according to the formats and storage provider the application needs.

