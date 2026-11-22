# The PDF Building Block

`CrestApps.OrchardCore.Pdf.Abstractions` and `CrestApps.OrchardCore.Pdf.Core` write every PDF in CrestApps.OrchardCore: business documents, checks, pay stubs, certificates and report exports. They are libraries referenced by modules, not features. A test bans other PDF libraries whose licenses do not allow them, so never add one, and never draw a QR code as an image.

Both projects use the `CrestApps.OrchardCore.Pdf` namespace (layout models in `CrestApps.OrchardCore.Pdf.Models`). Positions and sizes are points (72 per inch) from the top left of the page.

## A designed layout

For a document whose design is data (a certificate template, for one), describe the pages and hand them to `IPdfDocumentRenderer`:

```csharp
using CrestApps.OrchardCore.Pdf;
using CrestApps.OrchardCore.Pdf.Models;

// Startup (the extension is in Microsoft.Extensions.DependencyInjection): registers the renderer once, however many modules call it.
services.AddPdfDocumentRenderer();
```

```csharp
public sealed class BadgePrinter
{
    private readonly IPdfDocumentRenderer _renderer;

    public BadgePrinter(IPdfDocumentRenderer renderer)
    {
        _renderer = renderer;
    }

    public byte[] Print(string holderName, string verificationUrl)
    {
        var fonts = PdfFontSet.GetInstalled("Noto Sans");
        var page = new PdfPageLayout(PdfPageSize.Letter.Landscape);

        page.Elements.Add(new PdfTextElement
        {
            Text = holderName,
            X = 72,
            Top = 200,
            Width = 648,
            FontSize = 32,
            Bold = true,
            Alignment = PdfTextAlignment.Center,
        });

        page.Elements.Add(new PdfQrCodeElement { Value = verificationUrl, X = 640, Top = 440, Size = 96 });

        var layout = new PdfDocumentLayout
        {
            Title = "Certificate",
            RegularFont = fonts.Regular,
            BoldFont = fonts.Bold,
        };

        layout.Pages.Add(page);

        return _renderer.Render(layout);
    }
}
```

| Type | Members |
|---|---|
| `PdfDocumentLayout` | `Title`, `Author`, `RegularFont` and `BoldFont` (TrueType bytes, embedded as a subset; no bold file outlines the regular face), `Pages`. |
| `PdfPageLayout` | `Size`, `BackgroundColor`, `BackgroundImage` (JPEG or 8-bit PNG stretched behind the elements), `Elements` (drawn in order). |
| `PdfElement` | Base: `X`, `Top`. |
| `PdfTextElement` | `Text`, `Width` (0 writes each line at `X`), `FontSize` (12), `Bold`, `Color`, `Alignment` (`Left`, `Center`, `Right`), `MaxLines` (ellipsis on the last line; 0 for all). A missing glyph prints `?`. |
| `PdfImageElement` | `Data` (JPEG or 8-bit PNG) stretched to `Width` by `Height`. |
| `PdfLineElement` | (`X`, `Top`) to (`X2`, `Top2`), `Color`, `Thickness`. |
| `PdfRectangleElement` | `Width`, `Height`, optional `Fill`, optional `Outline` with `OutlineThickness`. |
| `PdfQrCodeElement` | `Value`, `Size` (72), `Color`; vector squares with the quiet zone on white. |

`PdfPageSize`: `Letter`, `Legal`, `A4`, `FromInches(width, height)` and `.Landscape`. `PdfColor`: `Black`, `White`, `FromRgb(uint)`, `TryParseHex("#1E3A8A", out color)`, `TryParse`, `Parse(value, fallback)`, `Lighten(amount)`.

## Documents that place or flow their own content

| Type | Use |
|---|---|
| `PdfDocumentWriter` (`IDisposable`) | Entry point: `AddRegularFont(fonts)`, `AddBoldFont(fonts)`, `AddFont(bytes)`, `AddStandardFont(bold)`, `AddSymbolFont(bytes, characters)`, `AddPage(size)` returns a `PdfCanvas`, `Build()` returns the bytes. |
| `PdfCanvas` | `DrawText`, `DrawTextRight`, `DrawTextOnBaseline`, `DrawLine`, `DrawPolyline`, `DrawRectangle`, `FillRectangle`, `FillPolygon`, `FillCircle`, `FillWedge`, `DrawImage` (returns `false` for an unsupported file). |
| `PdfFlow` | Flowing content: `new PdfFlow(writer, size, regular, bold, pageStarted)`; `Left`, `Width`, `Top`, `Remaining`, `EnsureSpace(height)` (new page when the block does not fit), `Advance`, `AddSpace`, `WriteLines`, `NewPage()`, and `WriteFooters("Page {0} of {1}", size, color)` once the content is laid out. Margin 40 points. |
| `PdfFontSet` | `PdfFontSet.GetInstalled(family)` finds a family or its stand-in (`PdfFontResolver` knows Windows, Linux and macOS file names); `Regular`, `Bold` bytes. |
| `PdfText` | `Clean(value)` (keeps line breaks, other control characters become spaces), `Wrap(text, font, size, width)`, `Fit(text, font, size, width)`. |
| `PdfImage` | `TryRead(bytes, out image)` reads PNG/JPEG size to keep a logo's proportions. |
| `PdfQrCode` | `Draw(canvas, value, x, top, size, color)` with medium error correction. |

```csharp
using CrestApps.OrchardCore.Pdf;

public sealed class StatementPdf
{
    public byte[] Write(string title, IReadOnlyList<string> lines)
    {
        using var writer = new PdfDocumentWriter(title);

        var fonts = PdfFontSet.GetInstalled("Arial");
        var regular = writer.AddRegularFont(fonts);
        var bold = writer.AddBoldFont(fonts);

        var flow = new PdfFlow(writer, PdfPageSize.A4, regular, bold);

        foreach (var line in lines)
        {
            var wrapped = PdfText.Wrap(line, regular, 10, flow.Width);

            flow.WriteLines(wrapped, regular, 10, PdfColor.Black);
        }

        flow.WriteFooters("Page {0} of {1}", 8, PdfColor.Black);

        return writer.Build();
    }
}
```

## Fonts

Every PDF embeds its font, so the font must be installed on the server. Sites printing letters beyond Latin install a family that has them (such as Noto Sans) and name it where the document lets you choose a font. Business Documents PDF uses Arial or its stand-in and falls back to the standard Helvetica (Latin-1 only) when none is usable.
