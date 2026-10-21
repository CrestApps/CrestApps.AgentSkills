---
name: crestapps-core-document-agents
description: Skill for the CrestApps.Core 2.0 document system agents that create, edit, preview and export files inside a conversation. Covers the Presentation Agent registered by AddOpenXml, the Word Agent registered by AddWord, the PDF Agent registered by AddPdf, and the Tabular Data Agent preview_tabular_data tool, their per-conversation workspaces in IDocumentFileStore, SVG previews returned as fig and doc markers with the download endpoint they need, PresentationAgentOptions, WordAgentOptions, PdfAgentOptions and the preview and composition options, PDF signing with AddPdfSigning and IPdfSigningCertificateProvider, IConversationWorkspaceCleanupHandler, generated file writers with AddGeneratedFileWriter and the requestable flag, and exposure of these agents through A2A and MCP.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Document Agents

Four code-defined **system agents** work on files for the primary model. Each is always available to orchestration, hidden from AI Profile and Chat Interaction agent pickers, published through an A2A host, and runs its own hidden tools. The primary model delegates every request that produces or changes that kind of file, and every follow-up ("make the title blue"), to the agent. Base document processing is in `crestapps-core-document-processing`.

| Agent | Name | Registered by | Package |
|---|---|---|---|
| Tabular Data Agent | `tabular-data-agent` | `AddDocumentProcessing()` | `CrestApps.Core.AI.Documents` |
| Presentation Agent | `presentation-agent` | `AddOpenXml()` | `CrestApps.Core.AI.Documents.OpenXml` (agent code in `CrestApps.Core.AI.Documents`) |
| Word Agent | `word-agent` | `AddWord()` | `CrestApps.Core.AI.Documents.Word` |
| PDF Agent | `pdf-agent` | `AddPdf()` on the document-processing builder | `CrestApps.Core.AI.Documents.Pdf` |

## Registration

```csharp
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Endpoints;
using CrestApps.Core.AI.Documents.OpenXml;
using CrestApps.Core.AI.Documents.Pdf;
using CrestApps.Core.AI.Documents.Word;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddChatInteractions(chatInteractions => chatInteractions.AddYesSqlStores())
        .AddDocumentProcessing(documentProcessing => documentProcessing
            .AddYesSqlStores()
            .AddOpenXml()          // Office readers and writers, Presentation Agent
            .AddWord()             // Word Agent (uses the OpenXml .docx support)
            .AddPdf()              // PDF reader and writer, PDF Agent, PDF export of decks
            .AddPdfSigning(options =>
            {
                options.CertificatePath = builder.Configuration["Pdf:CertificatePath"];
                options.CertificatePassword = builder.Configuration["Pdf:CertificatePassword"];
                options.TimestampAuthorityUrl = "https://timestamp.example.com";
            })
            .AddReferenceDownloads())));

var app = builder.Build();

app.AddDownloadAIDocumentEndpoint();
```

Previews are SVG pictures stored through `IGeneratedDocumentService` and served by `ai/documents/{documentId}/download`. Without `AddDownloadAIDocumentEndpoint()` (or without a writer for the preview format) a preview tool falls back to text, a Markdown table for tabular data, rather than emitting broken image markers.

## Workspaces

- Each chat session or chat interaction has a workspace per agent, stored through `IDocumentFileStore` and removed when the conversation is deleted or its history is cleared.
- **Uploads are never changed.** The first edit saves a working copy. Decks and Word documents are real `.pptx` and `.docx` files saved as a new version on every all-or-nothing tool call. PDF compositions are kept as a definition (page setup, theme, running heads, cover, table of contents, numbered blocks) and rendered on demand.
- `.potx` and `.dotx` uploads act as templates. Presentation revisions (`MaxRevisions`) back `undo_presentation_change` and `compare_presentations`.
- Word edits to one workspace are serialized by an in-process lock. On a multi-server deployment route a conversation to one server.

## Previews and Downloads

`preview_presentation`, `preview_word`, `preview_pdf` and `preview_tabular_data` return one `[fig:N]` marker per picture plus a `[doc:N]` download of the file they show. The chat client turns markers into a stacked carousel. An export of the same version later in the turn returns the same `[doc:N]`. A preview written as text gives no download.

The `.svg` preview writer is registered with `requestable: false`, so hosts can write it while `generate_file` and `export_tabular_data` can never be asked for model-authored SVG served from your origin.

## Options

```csharp
builder.Services.Configure<PresentationAgentOptions>(options => options.Enabled = true);
builder.Services.Configure<PresentationWorkspaceOptions>(options =>
{
    options.MaxDecks = 10;
    options.MaxRevisions = 5;
    options.MaxSlides = 200;
    options.MaxImageBytes = 15 * 1024 * 1024;
});
builder.Services.Configure<PresentationPreviewOptions>(options =>
{
    options.MaxSlides = 8;
    options.Width = 960;
});

builder.Services.Configure<WordAgentOptions>(options =>
{
    options.MaxWorkingDocuments = 40;
    options.MaxDocumentBytes = 50L * 1024 * 1024;
    options.MaxUncompressedDocumentBytes = 256L * 1024 * 1024; // zip-bomb guard for uploads
    options.Author = "AI Assistant";                           // signs comments and tracked changes
});
builder.Services.Configure<WordPreviewOptions>(options =>
{
    options.MaxPages = 4;
    options.MaxRenderPages = 12;
    options.MaxLayoutPages = 500;
});

builder.Services.Configure<PdfAgentOptions>(options =>
{
    options.MaxDocumentBytes = 50L * 1024 * 1024;
    options.MaxWorkingDocuments = 40;
    options.MaxVisionPagesPerCall = 8;
});
builder.Services.Configure<PdfCompositionOptions>(options =>
{
    options.DefaultPageSize = "A4";   // default
    options.DefaultMarginMm = 20;
    options.DefaultFontFamily = "Arial";
});
builder.Services.Configure<PdfPreviewOptions>(options => options.MaxPages = 4);

builder.Services.Configure<TabularPreviewOptions>(options =>
{
    options.MaxRows = 50;
    options.MaxColumns = 15;
});
```

Set `PresentationAgentOptions.Enabled`, `WordAgentOptions.Enabled` or `PdfAgentOptions.Enabled` to `false` to keep reading and writing the format (and `generate_file`) without the agent. While the Presentation Agent is enabled, `generate_file` hands `.pptx` requests to it.

The understanding tools of the PDF Agent (`summarize_pdf`, `ask_pdf`, `ocr_pdf` and others) use the `utility` deployment slot for text and the `vision` slot for scanned pages and pictures, and say so when none is configured. `generate_slide_image` uses the `image` slot.

## PDF Signing

`sign_pdf` signs only with an identity the host configures, never one supplied in the conversation. `AddPdfSigning()` loads a PKCS#12 file through `FilePdfSigningCertificateProvider`. To use a certificate store or key vault, register your own provider before `AddPdfSigning()` (its registration is a `TryAdd`).

```csharp
using System.Security.Cryptography.X509Certificates;
using CrestApps.Core.AI.Documents.Pdf;

public sealed class StorePdfSigningCertificateProvider : IPdfSigningCertificateProvider
{
    private readonly string _thumbprint;

    public StorePdfSigningCertificateProvider(IConfiguration configuration)
    {
        _thumbprint = configuration["Pdf:SigningCertificateThumbprint"];
    }

    public Task<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_thumbprint))
        {
            return Task.FromResult<X509Certificate2>(null); // sign_pdf reports that signing is not configured
        }

        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, _thumbprint, validOnly: false);

        return Task.FromResult(matches.Count > 0 ? matches[0] : null);
    }
}

builder.Services.AddSingleton<IPdfSigningCertificateProvider, StorePdfSigningCertificateProvider>();
```

`PdfSigningOptions` also has `DigestAlgorithm` (`SHA256`), `DefaultReason`, `DefaultLocation` and `ContactInfo`. `verify_pdf_signature` works without any signing setup.

## Cleaning Up Your Own Conversation State

A feature that keeps working state per conversation implements `IConversationWorkspaceCleanupHandler`. `DefaultConversationDocumentCleanupService` calls every handler after the conversation's documents and tabular data are removed, and a failing handler is logged without stopping the others.

```csharp
using CrestApps.Core.AI.Documents;
using Microsoft.Extensions.DependencyInjection.Extensions;

// IDiagramWorkspaceStore is the host's own store.
public sealed class DiagramWorkspaceCleanupHandler : IConversationWorkspaceCleanupHandler
{
    private readonly IDiagramWorkspaceStore _store;

    public DiagramWorkspaceCleanupHandler(IDiagramWorkspaceStore store)
    {
        _store = store;
    }

    public Task CleanupAsync(string referenceId, string referenceType, CancellationToken cancellationToken = default)
        => _store.DeleteForConversationAsync(referenceId, referenceType, cancellationToken);
}

builder.Services.TryAddEnumerable(
    ServiceDescriptor.Scoped<IConversationWorkspaceCleanupHandler, DiagramWorkspaceCleanupHandler>());
```

## Generated File Writers

`generate_file`, exports and agent downloads write through keyed `IGeneratedFileWriter` implementations.

```csharp
using CrestApps.Core.AI.Documents.Generation;

builder.Services.AddGeneratedFileWriter<RtfGeneratedFileWriter>(".rtf");                    // requestable
builder.Services.AddGeneratedFileWriter<SnapshotGeneratedFileWriter>(requestable: false, ".snap"); // host-only
```

- `IGeneratedFileWriter.WriteAsync(GeneratedFileContent content, Stream destination, CancellationToken)`.
- `GeneratedFileContent` carries `Title`, `Text`, `Header`, `Rows`, `Sheets` (multi-sheet workbooks), `SpreadsheetFormatting` and `EncodedContent`. Bytes in `EncodedContent` are stored as given without a writer.
- `GeneratedDocumentRequest.ContentType` overrides the media type a stored file is served with.
- Only requestable extensions appear in `IGeneratedFileWriterResolver.SupportedExtensions`, which `generate_file` and `export_tabular_data` gate on.

## Exposure Outside the Chat

- An A2A host that builds cards from `IAIProfileManager.GetAsync(AIProfileType.Agent)` publishes these agents too (see `crestapps-core-a2a`). Filter them out if remote clients should not reach them.
- Over MCP they are reachable only when allow-listed in `McpServerOptions.Agents` or when `ExposeAllAgents` is `true` (see `crestapps-core-mcp`).

## Upgrading from 1.x

- The agents are new in 2.0. Hosts that already call `AddOpenXml()` or `AddPdf()` gain the Presentation or PDF Agent automatically; set the matching `Enabled = false` to keep 1.x behavior.
- `AddWord()` is a new opt-in package.
- Generated PDFs, `.docx` files and tabular exports now render Markdown and HTML structure, number formats, totals and charts instead of literal text.
