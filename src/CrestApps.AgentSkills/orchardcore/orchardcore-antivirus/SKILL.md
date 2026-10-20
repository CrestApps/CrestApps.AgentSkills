---
name: orchardcore-antivirus
description: Skill for scanning and validating uploaded files in Orchard Core 4.0. Covers the OrchardCore.Antivirus.ClamAV feature, the OrchardCore Antivirus ClamAV configuration section, clamd connectivity and failure behavior, and the shared file upload security pipeline with IFileEventHandler, FileCreatingContext, FileCreatingResult and FileCreationService CreateAsync and CreatedAsync for custom upload code, including stream ownership and custom file validation handlers. Use this skill when requests mention Orchard Core antivirus, ClamAV, clamd, malware scanning, virus scanning, file upload security, rejecting uploads, validating uploaded files, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Antivirus, OrchardCore.Antivirus.ClamAV, ClamAvOptions, IFileEventHandler, FileCreationService, FileCreatingContext, FileCreatingResult, IMediaFileStore, and DefaultMediaFileStore.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Antivirus and File Upload Security - Prompt Templates

## Scan Uploads Before They Are Stored

You are an Orchard Core expert. Generate configuration and code that inspects user-uploaded files before Orchard Core stores them permanently.

### Guidelines

- Orchard Core 4.0 runs every upload through a shared pre-storage pipeline: `FileCreationService` calls each registered `IFileEventHandler.CreatingAsync()` before the file is written and `CreatedAsync()` after it is stored.
- Any handler that returns a failed `FileCreatingResult` aborts the upload; the file must not be stored.
- Built-in upload surfaces already use the pipeline: media uploads (through `DefaultMediaFileStore`) and deployment package imports (local and remote), which are scanned before the archive is written to temporary storage.
- `OrchardCore.Antivirus.ClamAV` adds a ClamAV scanner as an `IFileEventHandler`. It fails closed: malware detections and scanner connectivity, timeout, or protocol failures all reject the upload.
- Custom code that accepts a file and stores it outside `IMediaFileStore` must call `FileCreationService.CreateAsync()` before writing and `CreatedAsync()` after writing. Never save first and scan afterwards.
- Do not call `FileCreationService` again when uploading through `IMediaFileStore.CreateFileFromStreamAsync()`; the media store already does.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling ClamAV

```json
{
  "steps": [
    {
      "name": "feature",
      "enable": [
        "OrchardCore.Antivirus.ClamAV"
      ]
    }
  ]
}
```

The feature appears as **ClamAV Antivirus Scanner** in the Security category. Configure the connection before enabling it; until `clamd` is reachable, every upload is rejected.

### ClamAV Configuration

The section is `OrchardCore:Antivirus:ClamAV` (new in 4.0, no legacy name) and can be set per tenant:

```json
{
  "OrchardCore": {
    "Antivirus": {
      "ClamAV": {
        "Host": "clamav",
        "Port": 3310,
        "ConnectTimeoutSeconds": 5,
        "TransferTimeoutSeconds": 30
      }
    }
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `Host` | none | Host name or IP of the `clamd` service. Required. |
| `Port` | `3310` | `clamd` TCP port. |
| `ConnectTimeoutSeconds` | `5` | Connection timeout. |
| `TransferTimeoutSeconds` | `30` | Timeout for streaming the file and reading the verdict. Raise it for large uploads. |

Environment variables:

```text
OrchardCore__Antivirus__ClamAV__Host=clamav
OrchardCore__Antivirus__ClamAV__Port=3310
```

The connection is reused per configuration instead of opening a TCP client per scan. A detection returns a failed `FileCreatingResult`; connectivity, timeout, and protocol failures throw `AntivirusScanningException`, so custom upload code must treat both as a rejected upload. Non-seekable upload streams are buffered to tenant temporary storage (`ITempDirectoryProvider`) before scanning.

### Running clamd with Docker Compose

```yaml
services:
  clamav:
    image: clamav/clamav:stable
    ports:
      - "3310:3310"
  cms:
    image: my-orchard-site
    environment:
      OrchardCore__Antivirus__ClamAV__Host: clamav
    depends_on:
      - clamav
```

`clamd` limits stream size with its own `StreamMaxLength` setting; keep it at least as large as the Media `MaxFileSize`.

### Storing an Upload from Custom Code

```csharp
using Microsoft.AspNetCore.Http;
using OrchardCore.FileStorage;

namespace MyModule.Services;

public sealed class AttachmentUploadService
{
    private readonly FileCreationService _fileCreationService;
    private readonly IFileStore _fileStore;

    public AttachmentUploadService(
        FileCreationService fileCreationService,
        IFileStore fileStore)
    {
        _fileCreationService = fileCreationService;
        _fileStore = fileStore;
    }

    public async Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var uploadedStream = file.OpenReadStream();
        await using var result = await _fileCreationService.CreateAsync(
            new FileCreatingContext(file.FileName, file.Length, file.ContentType),
            uploadedStream,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new FileStoreException(result.ErrorMessage
                ?? $"The uploaded file '{file.FileName}' was rejected before it could be stored.");
        }

        // Store the processed stream while the result is still in scope.
        var path = await _fileStore.CreateFileFromStreamAsync(file.FileName, result.Stream);
        var fileInfo = await _fileStore.GetFileInfoAsync(path);

        await _fileCreationService.CreatedAsync(fileInfo, cancellationToken);

        return path;
    }
}
```

Register the service as you normally would. `FileCreationService` is registered (`TryAddTransient`) by the Media and Deployment features; when your feature can run without them, add `services.TryAddTransient<FileCreationService>();` in its startup.

### Stream Ownership

- `CreateAsync()` owns the original upload stream by default and disposes the final stream when the returned `FileCreatingResult` is disposed. Pass `leaveOpen: true` to keep ownership of the original stream.
- A handler may replace the stream (for example after stripping metadata); the service disposes superseded intermediate streams.
- Keep the result alive with `await using` for as long as you read `result.Stream`, and dispose a failed result the same way.

### Writing a Custom File Validation Handler

Return `FileCreatingResult.Failed(...)` to reject, or `FileCreatingResult.Success(stream)` to continue with the same or a replacement stream.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.FileStorage;
using OrchardCore.Infrastructure;

namespace MyModule.Services;

public sealed class RejectExecutableFileEventHandler : IFileEventHandler
{
    private static readonly HashSet<string> s_blockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1",
    };

    public Task<FileCreatingResult> CreatingAsync(
        FileCreatingContext context,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (s_blockedExtensions.Contains(Path.GetExtension(context.FileName)))
        {
            return Task.FromResult(FileCreatingResult.Failed(stream, new ResultError
            {
                Message = new LocalizedString(nameof(RejectExecutableFileEventHandler), "Executable files are not allowed."),
            }));
        }

        return Task.FromResult(FileCreatingResult.Success(stream));
    }

    public Task CreatedAsync(IFileStoreEntry fileInfo, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.FileStorage;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFileEventHandler, RejectExecutableFileEventHandler>());
    }
}
```

`FileCreatingContext` exposes `Path`, `FileName`, `Length`, and `ContentType`. Handlers run in registration order and the pipeline stops at the first failure. A handler that reads the stream must leave it readable for the next handler (rewind a seekable stream, or return a buffered replacement).

### Related Skills

- `orchardcore-media` covers `AllowedFileExtensions`, `RestrictedFileExtensions`, and `MaxFileSize`, which are enforced in addition to this pipeline.
- `orchardcore-temp-file-storage` covers where in-progress uploads are written; temporary storage is not scanned, so still run uploads through `FileCreationService` before storing them permanently.

### Troubleshooting

| Symptom | Check |
|---|---|
| Every upload fails after enabling ClamAV | `Host` is missing or `clamd` is unreachable from the app container. |
| Large uploads fail with a scanner error | Increase `TransferTimeoutSeconds` and the `clamd` `StreamMaxLength`. |
| Custom upload is not scanned | The code writes directly to storage; route it through `FileCreationService.CreateAsync()` or `IMediaFileStore`. |
| Files are scanned twice | Remove the extra `FileCreationService` call around `IMediaFileStore.CreateFileFromStreamAsync()`. |
