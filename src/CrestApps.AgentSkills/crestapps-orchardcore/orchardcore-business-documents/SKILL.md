---
name: orchardcore-business-documents
description: Skill for the CrestApps Orchard Core Business Documents module (CrestApps.OrchardCore.BusinessDocuments and BusinessDocuments.Pdf) and the shared PDF library (CrestApps.OrchardCore.Pdf.Core). Covers the neutral BusinessDocument model, AddBusinessDocumentKind, document templates with IBusinessDocumentTemplateManager GetForKindAsync and ApplyAsync, IBusinessDocumentHtmlRenderer for emails, the BusinessDocument shape and its per-kind alternates, IBusinessDocumentPdfRenderer with logos and font fallback, number series with AddNumberSeries, INumberSeries and NumberSeriesFormat, the BusinessDocumentTemplates recipe and deployment step, permissions, and IPdfDocumentRenderer, PdfDocumentWriter, PdfFlow and PdfCanvas. Use this skill when requests mention printable invoices, receipts, estimates or credit notes, document templates, a document PDF, document numbering formats, or writing a PDF from a module. Strong matches include BusinessDocument, BusinessDocumentTemplate, NumberSeriesSettings and PdfFlow.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Business Documents

You are an Orchard Core expert. Generate accurate setup, recipes and extension code for the Business Documents module: one neutral document model that every module (Receipts, Transactions, Invoicing) fills, one template catalog that gives documents the business's look, one e-mail-safe HTML renderer, one themeable shape and one PDF renderer. It also owns the number series settings page and sits on the shared PDF library (`CrestApps.OrchardCore.Pdf.Core`).

Related skills: `orchardcore-receipts` (receipts printed as `Receipt` documents), `orchardcore-transactions` (`PaymentRequest` documents and the `Financial.*` number series), `orchardcore-invoicing` (`Invoice`, `CreditNote`, `Estimate` and statement documents), `orchardcore-payroll` and `orchardcore-banking` (pay stubs and checks written directly with Pdf.Core), `orchardcore-reports` (PDF exports).

## Guidelines

- Install `CrestApps.OrchardCore.BusinessDocuments` in the web/startup project. A module that prints documents references `CrestApps.OrchardCore.BusinessDocuments.Abstractions` (model, contracts, `AddBusinessDocumentKind`, permissions) and, to render without the module, `CrestApps.OrchardCore.BusinessDocuments.Core` (`AddBusinessDocumentsCore()`, the HTML renderer, the template store and manager).
- Every value of a `BusinessDocument` is text already written for the reader: format money with `IMoneyFormatter` and dates with `ILocalClock` before you set them. Renderers never compute or format anything.
- Register each kind you print with `AddBusinessDocumentKind`, so the template editor lists it. Kinds are open strings.
- Call `IBusinessDocumentTemplateManager.ApplyAsync` before rendering: the template only fills what the document leaves empty.
- Resolve `IBusinessDocumentPdfRenderer` as optional (`GetService`): it exists only with the **Business Documents PDF** feature.
- Never reference another PDF library or copy drawing, font or text-wrapping code: use `CrestApps.OrchardCore.Pdf.Core`.
- All recipe JSON is wrapped in `{ "steps": [...] }`; all C# classes are `sealed` except view models.

## Features

| Feature | ID | Depends on |
|---|---|---|
| Business Documents | `CrestApps.OrchardCore.BusinessDocuments` | none |
| Business Documents PDF | `CrestApps.OrchardCore.BusinessDocuments.Pdf` | Business Documents |

Receipts depends on Business Documents; Invoicing depends on both. Constants: `BusinessDocumentsConstants.Features.Area`, `.Features.Pdf`, `ShapeType` (`BusinessDocument`) and `NumberSeriesSettingsGroupId` (`number-series`).

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.BusinessDocuments",
        "CrestApps.OrchardCore.BusinessDocuments.Pdf"
      ],
      "disable": []
    }
  ]
}
```

## The document model

`BusinessDocument` (`CrestApps.OrchardCore.BusinessDocuments.Models`):

| Member | Holds |
|---|---|
| `Kind` | The registered kind, such as `Invoice`. Picks the template and the shape alternate. |
| `Title`, `Number` | Heading and number (a draft may have none). |
| `Culture` | Culture to render in, such as `fr-FR` (renderers use a `CultureScope`); `null` keeps the current one. |
| `Status` | `BusinessDocumentStatus(Label, Tone)`; `BusinessDocumentTone` is `Neutral`, `Success`, `Warning`, `Danger`, `Info`. |
| `Watermark` | Text across every page, such as `DRAFT` or `VOID`. |
| `Issuer`, `Recipient`, `ShipTo` | `BusinessDocumentParty`: `Name`, `Lines` (address lines), `TaxId`, `Email`, `Phone`, `Website`. |
| `RecipientTitle`, `ShipToTitle` | Party block titles; `null` prints the localized "Bill to" / "Ship to". |
| `Fields` | `BusinessDocumentField(Label, Value)` printed near the top (dates, reference, terms). |
| `Columns` | `BusinessDocumentColumn(Name, Title, Alignment, Width)`; `BusinessDocumentAlignment` `Start`, `Center`, `End`; `Width` 0 sizes to content. |
| `Rows` | `BusinessDocumentRow`: `Kind` (`Item`, `Section`, `Subtotal`, `Note`), `Cells` keyed by column name, `Detail` (small text under the first cell). |
| `Totals` | `BusinessDocumentTotal(Label, Value, emphasized, muted)`; emphasized is the total or amount due, muted a tax line. |
| `Sections` | `BusinessDocumentSection(Title, Text)` after the totals (notes, terms, payment instructions). |
| `Footer`, `LogoUrl`, `AccentColor`, `PaperSize` | The look. `AccentColor` must be `#RRGGBB` (anything else is ignored). `BusinessDocumentPaperSize` is `Default`, `Letter`, `A4`; `Default` takes the template's, else Letter. |

## Registering a kind and printing

```csharp
using CrestApps.OrchardCore.BusinessDocuments;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace Contoso.Registrations;

public sealed class Startup : StartupBase
{
    private static readonly LocalizationSource _kindTitle = LocalizationSource.Create<Startup>("Registration confirmations");

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddBusinessDocumentKind("RegistrationConfirmation", _kindTitle);
        services.AddScoped<RegistrationConfirmationPrinter>();
    }
}
```

```csharp
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Contoso.Registrations;

public sealed class RegistrationConfirmationPrinter
{
    private readonly IBusinessDocumentTemplateManager _templates;
    private readonly IBusinessDocumentHtmlRenderer _htmlRenderer;
    private readonly IServiceProvider _serviceProvider;

    public RegistrationConfirmationPrinter(
        IBusinessDocumentTemplateManager templates,
        IBusinessDocumentHtmlRenderer htmlRenderer,
        IServiceProvider serviceProvider)
    {
        _templates = templates;
        _htmlRenderer = htmlRenderer;
        _serviceProvider = serviceProvider;
    }

    public async Task<(string Html, byte[] Pdf)> PrintAsync(string number, string familyName, string amountDue, CancellationToken cancellationToken)
    {
        var document = new BusinessDocument
        {
            Kind = "RegistrationConfirmation",
            Title = "Registration confirmation",
            Number = number,
            Status = new BusinessDocumentStatus("Due", BusinessDocumentTone.Warning),
            Recipient = new BusinessDocumentParty { Name = familyName },
            Columns =
            [
                new BusinessDocumentColumn("Description", "Description"),
                new BusinessDocumentColumn("Amount", "Amount", BusinessDocumentAlignment.End),
            ],
        };

        document.Rows.Add(new BusinessDocumentRow { Cells = { ["Description"] = "Season fee", ["Amount"] = amountDue } });
        document.Totals.Add(new BusinessDocumentTotal("Amount due", amountDue, emphasized: true));

        // Fills the logo, accent color, paper size and footer the document leaves empty.
        await _templates.ApplyAsync(document, templateId: null, cancellationToken);

        var html = await _htmlRenderer.RenderAsync(document, cancellationToken);

        // Registered only by the Business Documents PDF feature.
        var pdfRenderer = _serviceProvider.GetService<IBusinessDocumentPdfRenderer>();
        var pdf = pdfRenderer is null ? null : await pdfRenderer.RenderAsync(document, cancellationToken);

        return (html, pdf);
    }
}
```

| Output | Use |
|---|---|
| `IBusinessDocumentHtmlRenderer.RenderAsync` | Self-contained HTML for an e-mail body: tables and inline styles, no stylesheet or script. Values are HTML-encoded; a logo with a scheme other than `http`/`https` is dropped; the watermark is a banner. |
| `BusinessDocument` shape | A Bootstrap card for a page with print styles: `@await DisplayAsync(await New.BusinessDocument(Document: document))`. Alternate `BusinessDocument__{Kind}` (letters, digits and underscores of the kind), so a theme overrides one kind with `BusinessDocument-Receipt.cshtml`. |
| `IBusinessDocumentPdfRenderer.RenderAsync` | PDF bytes: header with logo, accent rule, parties and fields, a line table whose accent header repeats on every page, totals, sections, footer, **Page X of Y**, watermark behind the content. A row moves to the next page whole. |

PDF logos are read only from the media library (PNG or JPEG checked from content, at most 2 MB; `/media/logo.png`, a full media URL or a media path). The server never fetches other URLs. Without the **Media** feature PDFs have no logo. Fonts: the installed Arial or its stand-in; with no usable font the PDF falls back to Helvetica (Latin-1 only), counted on the `crestapps.business_document.pdf.font_fallbacks` metric (meter `CrestApps.OrchardCore.BusinessDocuments`) with one warning logged.

## Document templates

`BusinessDocumentTemplate` is a catalog record (YesSql collection `BusinessDocuments`, `BusinessDocumentTemplateIndex`) managed through `IBusinessDocumentTemplateManager` (an `ICatalogManager<BusinessDocumentTemplate>`): `Name` (required), `Description`, `Kinds` (empty for every kind), `IsDefault`, `LogoUrl`, `AccentColor`, `PaperSize`, `Footer`, plus created, modified and owner stamps.

- `GetForKindAsync(kind, templateId)` returns the named template when it is for the kind; otherwise the default for the kind (a default listing the kind beats a default for every kind); otherwise `null`.
- `ApplyAsync(document, templateId)` copies the template's logo, accent color, paper size and footer where the document has none.
- Saving a default in the admin clears the default flag of the other templates claiming the same kinds.
- Add fields to the template editor with a `DisplayDriver<BusinessDocumentTemplate>`.

## Number series

Formatted, gap-free numbers come from `INumberSeries` (`CrestApps.OrchardCore.Numbering`, in `CrestApps.OrchardCore.Abstractions`; registered with `services.AddNumberSequence()` from YesSql.Core). A feature registers each series it issues so **Settings > Business documents > Number series** lists it:

```csharp
services.AddNumberSequence();
services.AddNumberSeries(
    "Registrations.Confirmation",
    LocalizationSource.Create<Startup>("Registration confirmations"),
    new NumberSeriesFormat("REG-{yyyy}-{seq:0000}", firstNumber: 1, NumberSeriesReset.Yearly));

// At issue time, in the caller's unit of work:
var number = await _numberSeries.NextAsync("Registrations.Confirmation", new NumberSeriesRequest { Date = today });
document.Number = number.Text;
```

- Tokens: `{seq}`, `{seq:0000}` (minimum digits), `{yyyy}`, `{yy}`, `{MM}` (date of issue in the site time zone). Exactly one `{seq}`; a series that resets yearly or monthly needs the date tokens that keep periods apart (`NumberSeriesFormat.Validate()`); patterns at most 40 characters.
- `NumberSeriesReset`: `Never`, `Yearly`, `Monthly`. `NumberSeriesRequest` has `Date`, `Scope` and `SeedAsync` (highest number already used, read once when the series is new). `NextAsync` returns `SeriesNumber(Sequence, Text)`.
- Registered series include `Financial.Receipt` (`R-{seq}`), `Financial.Invoice` (`INV-{seq}`), `Financial.CreditNote` (`CN-{seq}`), `Financial.RefundDocument` (`RF-{seq}`), all from 1001 (Transactions), and `Invoicing.Estimate` (`EST-{seq}` from 1001, Invoicing Estimates).
- `Validate()` returns a `NumberSeriesFormatError`: `None`, `Empty`, `TooLong`, `MissingSequence`, `SeveralSequences`, `UnknownToken`, `ResetNeedsYear`, `ResetNeedsMonth`, `FirstNumberTooLow`.
- The site's choices are stored in `NumberSeriesSettings.Formats`; a series set back to its registered format is removed so it follows the registration again. Nothing saves while a format has an error.

```json
{
  "steps": [
    {
      "name": "Settings",
      "NumberSeriesSettings": {
        "Formats": {
          "Financial.Invoice": { "Pattern": "INV-{yyyy}-{seq:0000}", "FirstNumber": 1, "Reset": 1 }
        }
      }
    }
  ]
}
```

`Reset` serializes as `0` (Never), `1` (Yearly) or `2` (Monthly).

## Recipes and deployment

The `BusinessDocumentTemplates` step (entries in `Templates`) creates or updates templates by `ItemId`; the **Document templates** deployment step exports them in the same shape, without the created, modified and owner members. `PaperSize` is `0` (Default), `1` (Letter) or `2` (A4). With CrestApps Recipes on, the step and the `NumberSeriesSettings` site settings are described to the recipe schema.

```json
{
  "steps": [
    {
      "name": "BusinessDocumentTemplates",
      "Templates": [
        {
          "ItemId": "4bdt0000000000000000000001",
          "Name": "Standard",
          "Kinds": [ "Invoice", "Receipt" ],
          "IsDefault": true,
          "LogoUrl": "/media/logo.png",
          "AccentColor": "#2563EB",
          "PaperSize": 2,
          "Footer": "Thank you for your business."
        }
      ]
    }
  ]
}
```

## Admin menu and permissions

| Menu | Permission |
|---|---|
| **Settings > Business documents > Document templates** | `ManageBusinessDocumentTemplates` (Manage business document templates) |
| **Settings > Business documents > Number series** (site settings group `number-series`) | `ManageNumberSeries` (Manage number series), registered for that settings group |

Both are granted to Administrator by default.

## The PDF building block

`CrestApps.OrchardCore.Pdf.Abstractions` (`IPdfDocumentRenderer`, layout models in `CrestApps.OrchardCore.Pdf.Models`, `PdfColor`, `PdfPageSize`) and `CrestApps.OrchardCore.Pdf.Core` (renderer and primitives on the Apache-2.0 PdfPig and QRCoder) are libraries, not features. Positions are points (72 per inch) from the top left. Details and samples: [references/pdf-building-block.md](references/pdf-building-block.md).

| Need | Use |
|---|---|
| A designed layout stored as data (certificates) | `services.AddPdfDocumentRenderer()` then `IPdfDocumentRenderer.Render(PdfDocumentLayout)` |
| Content placed at exact positions (checks, pay stubs, forms) | `PdfDocumentWriter` + `PdfCanvas` |
| Content that flows over pages (invoices, statements) | `PdfFlow` over a `PdfDocumentWriter` |

## Troubleshooting

| Symptom | Check |
|---|---|
| A kind is missing from the template editor's **Used for** list | The feature that registers it (`AddBusinessDocumentKind`) is off. |
| Template look not applied | `ApplyAsync` not called, the template is not for the kind and not a default, or the document already sets the value. |
| PDF has no logo | Business Documents PDF uses the media library only: Media off, file not PNG/JPEG, over 2 MB, or outside the media library. |
| PDF shows `?` or boxes for non-Latin text | No usable font installed; install a family that has the glyphs (such as Noto Sans) and watch the font fallback metric. |
| `IBusinessDocumentPdfRenderer` is null | **Business Documents PDF** is off. |
| Number series page refuses to save | A pattern without exactly one `{seq}`, an unknown token, or a resetting series without its date tokens. |
