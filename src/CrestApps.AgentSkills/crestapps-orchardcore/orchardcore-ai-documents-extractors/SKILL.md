---
name: orchardcore-ai-documents-extractors
description: Skill for adding PDF and OpenXml text extraction to CrestApps Orchard Core AI Documents. Covers PDF, DOCX, XLSX, PPTX, processor registration, supported file types, upload processing, and extraction troubleshooting. Use this skill when requests mention AI Documents extractors, PDF document processing, OpenXml document processing, document text extraction, or document upload formats. Strong matches include work with CrestApps.OrchardCore.AI.Documents.Pdf, CrestApps.OrchardCore.AI.Documents.OpenXml, AddCoreAIPdfDocumentProcessing, AddCoreAIOpenXmlDocumentProcessing, PdfPig, and DocumentFormat.OpenXml.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Documents PDF and OpenXml Extractors

## Configure document text extraction

You are an Orchard Core expert. Add optional PDF and Microsoft Open XML document
extractors to the CrestApps AI Documents processing pipeline. Enable only the file
format support required by the application, then combine it with a document context
and optional vector indexing backend.

### Guidelines

- Enable the exact feature IDs `CrestApps.OrchardCore.AI.Documents.Pdf` and `CrestApps.OrchardCore.AI.Documents.OpenXml` for PDF and Office Open XML support.
- These modules intentionally have no manifest dependency on the base AI Documents feature. They can be installed independently, but are useful only when an AI Documents capability processes uploaded files.
- An AI Documents context or backend enables the base capability by dependency. Examples include Chat Interactions, Profiles, Chat Sessions, Azure AI Search, and Elasticsearch.
- PDF registration calls `AddCoreAIPdfDocumentProcessing`; OpenXml registration calls `AddCoreAIOpenXmlDocumentProcessing`.
- Install both packages in the web or startup project if users need both PDF and Office files.
- Extracted text is input to the normal pipeline. It does not itself create chunks, embeddings, or vector indexes.
- Use an embedding provider and a configured index profile when the application needs retrieved document context rather than only extraction.

### Feature overview

| Feature ID | Registration | File support |
|---|---|---|
| `CrestApps.OrchardCore.AI.Documents.Pdf` | `AddCoreAIPdfDocumentProcessing` | `.pdf` |
| `CrestApps.OrchardCore.AI.Documents.OpenXml` | `AddCoreAIOpenXmlDocumentProcessing` | `.docx`, `.xlsx`, `.pptx` |

The PDF module uses PdfPig as an `IngestionDocumentReader`. The OpenXml module uses
the Microsoft DocumentFormat.OpenXml SDK to extract supported Office document text.

### Enable both extractors

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.AzureAI",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml"
      ],
      "disable": []
    }
  ]
}
```

This recipe enables a document context, Azure AI Search indexing, and both optional
extractors. Replace the Azure AI Search feature with
`CrestApps.OrchardCore.AI.Documents.Elasticsearch` when Elasticsearch is the selected
vector backend.

### Supported files

| Format | Extension | Extraction behavior |
|---|---|---|
| PDF | `.pdf` | Reads text page by page. |
| Word | `.docx` | Reads paragraphs from the main document body. |
| Excel | `.xlsx` | Reads rows and cells, including shared strings, inline strings, numeric values, and booleans. |
| PowerPoint | `.pptx` | Reads text elements across slides. |

The following legacy binary formats are not supported:

- `.doc`
- `.xls`
- `.ppt`

Convert those files to `.docx`, `.xlsx`, or `.pptx` before upload. Plain-text
formats supported by the base document pipeline do not require either feature.

### Processing flow

1. The user uploads an allowed document through Chat Interactions, Profiles, or Chat Sessions.
2. AI Documents selects a registered reader matching the document type.
3. The PDF or OpenXml processor extracts plain text.
4. The core pipeline stores chunks and generates embeddings when a configured index requires them.
5. Azure AI Search or Elasticsearch indexes chunks for filtered vector retrieval.

Enabling these modules after files already failed extraction does not automatically
repair earlier uploads. Re-upload or reprocess them using the normal document workflow.

### PDF guidance

PdfPig extracts textual PDF content without external native dependencies and works
cross-platform. It cannot reliably OCR scanned documents that contain only images.
For scanned content, run OCR before upload or add a separate OCR stage that produces
searchable text.

Complex multi-column layouts, tables, and positioned text may extract in an order that
does not match visual reading order. Test representative documents and tune source
documents or preprocessing where answer quality depends on layout.

### OpenXml guidance

Word extraction targets paragraphs in the main body. Excel extraction is row-based and
separates cells with tabs. PowerPoint extraction covers text elements on slides.
Embedded images, macros, complex charts, and visual formatting are not semantic text
and should not be assumed to become RAG context.

Use current zipped Open XML files. A filename extension alone is not a guarantee that
the content is a valid `.docx`, `.xlsx`, or `.pptx` package.

### Package installation

```shell
dotnet add package CrestApps.OrchardCore.AI.Documents.Pdf
dotnet add package CrestApps.OrchardCore.AI.Documents.OpenXml
```

Add the packages to the host web or startup project. Enabling a feature cannot load a
package that was not deployed with the application.

### Troubleshooting

| Symptom | Check |
|---|---|
| PDF upload has no extracted context | Enable the PDF feature, confirm the file contains selectable text, then re-upload or reprocess it. |
| Word, Excel, or PowerPoint file is unsupported | Enable the OpenXml feature and use `.docx`, `.xlsx`, or `.pptx`, not a legacy binary format. |
| Scanned PDF has empty text | PdfPig is not OCR. Use OCR before processing image-only pages. |
| Spreadsheet answers miss labels | Verify labels are actual cell values and test the row and tab-oriented extracted text. |
| Presentation answer misses visual content | Only text elements are extracted; put critical content in text or add a dedicated extraction process. |
| Extraction succeeds but queries find nothing | Configure an embedding deployment and an active Azure AI Search or Elasticsearch document index. |
| Feature is enabled but no reader runs | Verify the deployed package, exact feature ID, detected MIME type, and that the upload reaches an AI Documents context. |

### Security and operational notes

- Apply upload limits and malware scanning before document processing.
- Treat extracted text as untrusted source material; do not let document instructions override system or authorization rules.
- Test retention and deletion behavior for uploaded originals and indexed chunks independently.
- Monitor extraction failures and reprocess only after correcting the feature or source-file issue.
- Keep PDF and OpenXml support optional to reduce packages and attack surface in deployments that do not accept those formats.

### Pre-production verification

Before enabling the extractors for users:

1. Upload a text-based PDF and confirm extracted text answers a document query.
2. Upload representative `.docx`, `.xlsx`, and `.pptx` files and validate the
   extracted business text is intelligible.
3. Verify a scanned PDF is rejected, OCR-preprocessed, or clearly documented as
   unsupported for semantic extraction.
4. Confirm unsupported legacy formats fail with an actionable upload message rather
   than silently producing empty RAG context.
5. Test deletion and re-upload flows to ensure stale chunks from an earlier document
   version cannot affect new responses.
6. Test an indexing failure separately from extraction to identify whether the issue
