---
name: orchardcore-attachments
description: Skill for the CrestApps Orchard Core Attachments module (CrestApps.OrchardCore.Attachments), files attached to any record. Covers IAttachmentService (AddAsync, ListAsync, ReadAsync, DeleteAsync, CreateSignedPath), AttachmentUpload and Attachment on a RecordRef owner, one upload policy that matches each file by its bytes through FileFormats, AttachmentOptions in CrestApps:Attachments, encrypted storage in App_Data or Azure Blob Storage, scanning through Orchard Core file events and IAttachmentScanner, IAttachmentAccessProvider per owner kind, the /attachments/{id} endpoint with signed links, and timeline entry files. Use this skill when requests mention attaching files to a record, ticket attachments, upload limits, allowed file types, signed download links or scanning uploads. Strong matches include Attachment, AttachmentUpload, AttachmentOptions, FileFormats, IAttachmentAccessService, AddAttachmentAccessProvider and AddAttachmentScanner.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Attachments

## Files on Any Record

You are an Orchard Core expert. Generate accurate configuration and extension code for the CrestApps Attachments module. It keeps the files attached to records (a `RecordRef` owner such as `TimelineEntry:{id}`): checked by their own bytes, scanned, encrypted at rest and served only to people the owner's module allows, or through a signed link. The timeline uses it for the files of notes, comments and ticket replies; the Tickets API and portal upload through it.

### Guidelines

- Install `CrestApps.OrchardCore.Attachments` in the web/startup project and enable the feature; Timeline and Tickets use it when it is on (no hard dependency).
- Reference `CrestApps.OrchardCore.Attachments.Abstractions` (`IAttachmentService`, `IAttachmentAccessProvider`, `IAttachmentScanner`, `FileFormats`, `AttachmentOptions`, registration helpers) from your module.
- `IAttachmentService` checks nothing about the user. Ask `IAttachmentAccessService` on a page that acts for a user; it asks the `IAttachmentAccessProvider` registered for the owner's kind. **A kind without a provider is refused.**
- Never trust a file name or declared content type: the format comes from the bytes.
- All C# classes must use the `sealed` modifier, except View Models.

### Feature

| Feature | ID | Depends on |
|---|---|---|
| Attachments | `CrestApps.OrchardCore.Attachments` | none |

Category **Organization**. No admin menu, permissions, recipe or deployment steps; files belong to their records.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Attachments"
      ],
      "disable": []
    }
  ]
}
```

| Project | Holds |
|---|---|
| `CrestApps.OrchardCore.Attachments.Abstractions` | `FileFormat`, `FileFormats`, `Attachment`, `AttachmentUpload`, `AttachmentResult`, `AttachmentOptions`, `IAttachmentService`, `IAttachmentScanner`, `IAttachmentAccessProvider`, `IAttachmentAccessService`, `AttachmentsConstants`, `AddAttachmentAccessProvider<T>()`, `AddAttachmentScanner<T>()` |
| `CrestApps.OrchardCore.Attachments.Core` | `AttachmentStore` and `AttachmentIndex` (collection `Attachments`), `IAttachmentFileStore`, `EncryptedAttachmentFileStore`, `DefaultAttachmentService`, `DefaultAttachmentAccessService`, `AddAttachmentsCore(configuration)` |
| `CrestApps.OrchardCore.Attachments` (module) | Migration, `FileCreationAttachmentScanner`, download endpoint |

## Upload Policy

`AttachmentOptions` binds from `CrestApps:Attachments` (`AttachmentsConstants.ConfigurationSection`) in the tenant's configuration:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Attachments": {
        "MaxFileBytes": 20971520,
        "MaxFilesPerUpload": 10,
        "AllowedFormats": [ "JPEG", "PNG", "PDF" ],
        "AzureBlob": {
          "ConnectionString": "UseDevelopmentStorage=true",
          "ContainerName": "attachments",
          "BasePath": "{{ ShellSettings.Name }}"
        }
      }
    }
  }
}
```

| Setting | Default | Meaning |
|---|---|---|
| `MaxFileBytes` | 20 MB | Largest file |
| `MaxFilesPerUpload` | 10 | Files in one `AddAsync` call |
| `AllowedFormats` | empty (all of `FileFormats.All`) | Names from `FileFormats`: `JPEG`, `PNG`, `GIF`, `WebP`, `PDF`, `Word`, `Excel`, `PowerPoint`, `Text`, `CSV`, `vCard` |
| `AzureBlob` (`CrestApps:Attachments:AzureBlob`) | none | `ConnectionString`, `ContainerName`, optional `BasePath`; without it files live in `App_Data/Sites/{tenant}/Attachments` |

- A format with a signature (images, PDF, Office) is matched by its bytes; an SVG or HTML page named `.png` is refused. The record keeps the format's media type and the original file name for downloads.
- Bytes are encrypted with the tenant's data protection keys; a file's storage name is a hash of its id. The files are removed with their tenant (`removeOnTenantRemoval`). `ContainerName` and `BasePath` may hold Liquid such as `{{ ShellSettings.Name }}`; the shared tenant blob options also take `CreateContainer` (default `true`), `RemoveContainer` and `RemoveFilesFromBasePath`.
- Messaging channels use the same `FileFormats` for the files they carry, with their own store.

## Attaching Files

```csharp
var result = await _attachmentService.AddAsync(
[
    new AttachmentUpload
    {
        Owner = new RecordRef("Inspection", inspectionId),
        FileName = file.FileName,
        DeclaredContentType = file.ContentType,
        Content = bytes,
        UploadedBy = new PartyRef(PartyKinds.User, userId),
    },
]);

if (!result.Succeeded)
{
    // result.Errors: too many files, too large, no allowed format matches, or a scanner refused it.
}
```

| `IAttachmentService` member | Does |
|---|---|
| `AddAsync(uploads)` | Checks every file before storing any, then stores them; returns `AttachmentResult { Attachments, Errors }` |
| `FindAsync(id)`, `GetAsync(ids)`, `ListAsync(owner)` | Load records (`Attachment`: `Owner`, `FileName`, `ContentType`, `Length`, `IsImage`, `UploadedBy`, `UploadedUtc`); `ListAsync` newest first |
| `ListUploadedBeforeAsync(ownerKinds, beforeUtc, fromUtc, take)` | Files of kinds uploaded before a time (the timeline's orphan sweeper uses it) |
| `ReadAsync(attachment)` | The decrypted bytes |
| `DeleteAsync(id)` | Removes the record and the bytes |
| `CreateSignedPath(id, lifetime)` | `/attachments/{id}?token=...` with the tenant's path base, for e-mails and guests |

## Scanning

The module runs every file through Orchard Core's `FileCreationService`, so each `IFileEventHandler` (such as the antivirus feature) can refuse it before it is stored. Add your own check with `services.AddAttachmentScanner<T>()`; every scanner must accept a file.

```csharp
public sealed class NoEmptyFilesScanner : IAttachmentScanner
{
    public Task<string> ScanAsync(AttachmentUpload upload, string contentType, CancellationToken cancellationToken = default)
        => Task.FromResult(upload.Content.Length == 0 ? "The file is empty." : null);
}

// Startup: services.AddAttachmentScanner<NoEmptyFilesScanner>();
```

Return `null` to accept, or the reason to refuse.

## Access and Downloads

```csharp
public sealed class InspectionAttachmentAccess : IAttachmentAccessProvider
{
    private readonly IAuthorizationService _authorizationService;

    public InspectionAttachmentAccess(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public string OwnerKind => "Inspection";

    public async Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
        => await _authorizationService.AuthorizeAsync(user, InspectionPermissions.ViewInspections);

    public async Task<bool> CanManageAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
        => await _authorizationService.AuthorizeAsync(user, InspectionPermissions.ManageInspections);
}

// Startup: services.AddAttachmentAccessProvider<InspectionAttachmentAccess>();
```

- `GET /attachments/{id}` serves a file to a signed-in user the owner's provider allows, or to anyone holding a valid signed link. Others are asked to sign in (whether or not the id exists); a signed-in user who may not read gets *not found*.
- Only pictures recognized by their bytes are shown inline; everything else, and `?download=true`, is a download. Responses carry `X-Content-Type-Options: nosniff`, no caching and a sandboxing content security policy.
- Timeline entry files have owner kind `TimelineEntry` (`TimelineConstants.AttachmentOwnerKind`) and are exactly as visible as the entry; redacting the entry removes them. The Timeline module registers that provider when Attachments is on.

## Troubleshooting

| Symptom | Check |
|---|---|
| Upload refused as an unknown format | The bytes match none of `AllowedFormats` (a renamed file, or a format not listed). |
| Download asks to sign in | No signed token, and the user is anonymous; or the link expired. |
| Download returns not found for a signed-in user | The owner kind has no `IAttachmentAccessProvider`, or it refused the user. |
| No file picker on the timeline composer or ticket forms | The Attachments feature is off. |
| Antivirus doesn't scan uploads | The antivirus feature must register an `IFileEventHandler`; Attachments calls it through `FileCreationService`. |
| Files lost on a new server | Files live in the tenant's `App_Data` unless `AzureBlob` is configured; data protection keys must travel too. |

Related skills: `orchardcore-timeline`, `orchardcore-tickets`, `orchardcore-antivirus`, `orchardcore-file-upload-security`, `orchardcore-omnichannel-messaging`.
