# Issued Certificates

`ICertificateIssuer` is part of the Skills feature. The **Skills: Issued Certificates** feature (`CrestApps.OrchardCore.Skills.IssuedCertificates`) adds templates, PDFs and the public verification page. The PDF is built with the PDF building block (`IPdfDocumentRenderer`, `CrestApps.OrchardCore.Pdf.Abstractions`).

## Certification Settings

With the feature on, the certification editor (**Content > Certification Types**, built through `IDisplayManager<Certification>`) shows an **Issued certificates** card (`IssuedCertificateSettings_Edit`, `Content:5`):

| Field | Property | Notes |
|---|---|---|
| Number the certificates | `GenerateNumbers` | Numbers come from `INumberSequence`, series `Skills:CertificateNumber:{prefix}`, six digits, seeded after the highest hand-recorded number with the same prefix. |
| Number prefix | `NumberPrefix` | Default `CERT-`. |
| Certificate template | `CertificateTemplateId` | A `CertificateTemplate` content item; without one the built-in default design prints. **Preview a certificate** opens `Admin/certifications/certificate-preview/{id}` (`ManageSkills`). |

Other modules add cards to the same editor with `DisplayDriver<Certification>` (Learning Certifications adds **Earned through learning**).

## Certificate Template Content Type

`CertificateTemplate` (creatable, listable, versionable, securable, not draftable) has `TitlePart` and `CertificateTemplatePart`. The part carries a `BackgroundImage` media field (one JPEG or PNG, up to 10 MB, stretched over the page) and these properties:

| Property | Default | Notes |
|---|---|---|
| `PageSize` | `Letter` | `Letter` or `A4`. |
| `Landscape` | `true` | |
| `FontFamily` | none | An installed font family, embedded in each PDF (for letters beyond Latin). |
| `AccentColor`, `TextColor` | `#1E3A8A`, `#111827` | |
| `ShowBorder`, `ShowQrCode` | `true` | The QR code links to the verification page. |
| `QrX`, `QrTop`, `QrSize` | `640`, `440`, `96` | Points. |
| `Blocks` | the default design | `CertificateTemplateBlock { Text (Liquid), X, Top, Width, FontSize = 14, Bold, Alignment (PdfTextAlignment, Center), Accent, MaxLines }`. |

Liquid values in a block:

| Object | Members |
|---|---|
| `Certificate` | `HolderName`, `Number`, `IssuedOn`, `ExpiresOn`, `Evidence`, `VerificationCode`, `VerificationUrl` |
| `Certification` | `Name`, `Issuer`, `Description` |

```text
{{ Certificate.HolderName }}
holds the {{ Certification.Name }} certification
Issued {{ Certificate.IssuedOn }}{% if Certificate.ExpiresOn %}  ·  Valid until {{ Certificate.ExpiresOn }}{% endif %}
{% if Certificate.Number %}Certificate No. {{ Certificate.Number }}{% endif %}
Verify this certificate at {{ Certificate.VerificationUrl }}
```

## Routes

| Route | Who | Does |
|---|---|---|
| `/certificates/verify` | anyone | Enter a code. |
| `/certificates/verify/{code}` | anyone | Holder, certification, issuer, number, dates and status; a revocation shows at once. |
| `/certificates/verify/{code}/certificate.pdf` | anyone | The certificate PDF (a revoked one is marked revoked). |
| `Admin/certificates/document/{id}` | `ViewSkills` | The PDF from the **Certificate** button on **Content > Certificates**. |
| `Admin/certifications/certificate-preview/{id}` | `ManageSkills` | A sample certificate for a certification. |

Every public page sends `X-Robots-Tag: noindex, nofollow` and is in the `Skills.CertificateVerification` rate-limit group (30 lookups a minute per IP when `OrchardCore.RateLimits` is on). Learning adds the PDF to the learner's **My certifications** page.

## Observers, Workflows and Webhooks

```csharp
public sealed class CertificateIssuedNotifier : ICertificationRecordObserver
{
    private readonly ILogger _logger;

    public CertificateIssuedNotifier(ILogger<CertificateIssuedNotifier> logger)
    {
        _logger = logger;
    }

    public Task IssuedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{Holder} earned {Certification} ({Number}).", context.Record.SubjectName, context.Certification?.Name, context.Record.Number);

        return Task.CompletedTask;
    }

    public Task RevokedAsync(CertificationRecordContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("{Number} was revoked: {Reason}.", context.Record.Number, context.Record.RevokedReason);

        return Task.CompletedTask;
    }
}

// Startup: services.AddScoped<ICertificationRecordObserver, CertificateIssuedNotifier>();
```

Observers run after the commit; a failing observer is logged and the others still run.

| Workflow event | Display name | Extra input |
|---|---|---|
| `CertificateIssuedEvent` | Certificate Issued | `IssuedOn`, `Source`, `SourceId`, `Actor` plus the certificate fields |
| `CertificateRenewedEvent` | Certificate Renewed | same, plus `PreviousRecordId` |
| `CertificateRevokedEvent` | Certificate Revoked | same, plus `Reason` |

Webhook events (CrestApps Webhooks): `skills.certificate.issued`, `skills.certificate.renewed`, `skills.certificate.revoked`, without the verification code.

## Personal Data

`SkillsPersonalDataContributor` exports a person's skills and certificates. Anonymizing keeps the certificates the site issued but removes the holder name and evidence.

## Recipe Fields

The `Skills` step carries the new fields: on a certification `generateNumbers`, `numberPrefix`, `certificateTemplateId`; on a certificate `source`, `sourceId`, `evidence`, `verificationCode`, `previousRecordId`, `renewedByRecordId`, `revokedReason`, `revokedBy`, `revokedUtc`.

```json
{
  "steps": [
    {
      "name": "Skills",
      "Certifications": [
        { "name": "First aid", "issuer": "Acme Academy", "validityMonths": 24, "generateNumbers": true, "numberPrefix": "FA-" }
      ]
    }
  ]
}
```
