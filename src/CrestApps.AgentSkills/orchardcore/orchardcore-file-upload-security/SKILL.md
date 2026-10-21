---
name: orchardcore-file-upload-security
description: Skill for securing user-uploaded files in Orchard Core 4.0 with the file upload security pipeline. Covers FileCreationService CreateAsync and CreatedAsync, aborting when FileCreatingResult Succeeded is false, stream ownership and leaveOpen, FileCreatingContext, custom IFileEventHandler checks for extensions, content types or file signatures, handler registration, which features register FileCreationService, the IMediaFileStore CreateFileFromStreamAsync overload taking a FileCreationService, and how Media API uploads, the media recipe step, TUS, deployment imports and XML-RPC use the pipeline. Use this skill when requests mention Orchard Core file upload security, validating, scanning or rejecting uploads, custom upload endpoints, or closely related implementation or troubleshooting work. Strong matches include OrchardCore.FileStorage, FileCreationService, FileCreatingContext, FileCreatingResult, IFileEventHandler, MediaFileStoreExtensions, IMediaFileStore, and FileStoreException.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core File Upload Security - Prompt Templates

## Run Uploads Through the Pre-Storage Pipeline

You are an Orchard Core expert. Generate code that runs every user-uploaded file through Orchard Core's file upload security pipeline before it is stored permanently, and handlers that inspect or reject those files.

### Guidelines

- `FileCreationService` (namespace `OrchardCore.FileStorage`, package `OrchardCore.FileStorage.Abstractions`) calls every registered `IFileEventHandler.CreatingAsync()` before a file is written, and `CreatedAsync()` after it was stored.
- Call `CreateAsync()` before any permanent write. When the returned `FileCreatingResult.Succeeded` is `false`, abort and do not store the file. Never save first and scan afterwards.
- Store `result.Stream`, not the original upload stream: a handler may have replaced it.
- Call `CreatedAsync()` only after the file was stored successfully, with the stored entry.
- Dispose the result with `await using`, whether it succeeded or failed, and keep it alive while you read `result.Stream`.
- Handlers can also throw (the ClamAV scanner throws `AntivirusScanningException`, a `FileStoreException`, when it cannot scan). Treat an exception as a rejected upload.
- For media, call the `IMediaFileStore.CreateFileFromStreamAsync()` overload that takes a `FileCreationService`. The plain `CreateFileFromStreamAsync(path, stream, overwrite)` only runs `IMediaCreatingEventHandler`, not this pipeline.
- Keep the existing checks too: authorization, `MediaOptions` allowed extensions and `MaxFileSize`. The pipeline adds to them.
- All C# classes must use the `sealed` modifier, except View Models.

### Core Types

| Type | Members |
|---|---|
| `FileCreationService` | `CreateAsync(context, stream, cancellationToken)`, `CreateAsync(context, stream, leaveOpen, cancellationToken)`, `CreatedAsync(IFileStoreEntry, cancellationToken)` |
| `FileCreatingContext` | Constructor `(path, length = null, contentType = null)`. `Path`, `FileName` (from `Path`), `Length`, `ContentType`, all settable by handlers except `FileName`. |
| `FileCreatingResult` | Derives from `OrchardCore.Infrastructure.Result` and is `IAsyncDisposable`. `Stream`, `Succeeded`, `Errors`, `ErrorMessage` (first non-empty error). Factories `Success(stream)` and `Failed(stream = null, params ResultError[] errors)`. |
| `IFileEventHandler` | `Task<FileCreatingResult> CreatingAsync(FileCreatingContext, Stream, CancellationToken)`, `Task CreatedAsync(IFileStoreEntry, CancellationToken)` |

Pipeline behavior:

- Handlers run in registration order. The first failed result stops the pipeline and is returned.
- A handler that returns `null` makes `CreateAsync()` throw `InvalidOperationException`. A result whose `Stream` is `null` keeps the current stream.
- With no handlers registered, `CreateAsync()` returns a successful result over the original stream and `CreatedAsync()` does nothing.

### Where Orchard Core Uses the Pipeline

| Upload surface | How it uses the pipeline |
|---|---|
| Media Library uploads (`api/media` upload endpoint, used by the admin Media Gallery and media fields) | Calls `IMediaFileStore.CreateFileFromStreamAsync(fileCreationService, ...)` for each file after the extension and folder permission checks. A rejection is returned as the file's `Error`. |
| `media` recipe step | Stores each file through the same overload, so files imported by recipes and deployment packages are checked. |
| XML-RPC `metaWeblog.newMediaObject` (`OrchardCore.RemotePublishing`) | Stores the file through the same overload. |
| Deployment package import (**Tools → Deployments → Package Import**) | Runs `CreateAsync()` on the uploaded `.zip` or `.json` before copying it to a temporary archive. A rejection shows an error notification. `CreatedAsync()` is not called because the package is never stored permanently. |
| Remote deployment import endpoint (`OrchardCore.Deployment.Remote`) | Same as the package import; a rejection returns `400 Bad Request` with the error message. |
| TUS resumable uploads (`OrchardCore.Media.Tus`) | On completion the file is moved with the plain `IMediaFileStore.CreateFileFromStreamAsync(path, stream)`, so it does not run this pipeline in the current source. Check the Orchard Core version you target before relying on TUS uploads being scanned. |

`FileCreationService` is registered with `TryAddTransient` by the `OrchardCore.Media`, `OrchardCore.Deployment` and `OrchardCore.Deployment.Remote` features. A feature that can run without them must register it itself.

### Storing an Upload from Custom Code

Use this for custom controllers, minimal APIs, admin endpoints, importers, or background flows that accept a file and store it anywhere other than the media library.

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
        // Needed when neither Media nor Deployment is guaranteed to be enabled.
        services.TryAddTransient<FileCreationService>();
        services.AddScoped<AttachmentUploadService>();
    }
}
```

Return the rejection to the user instead of throwing when that fits the endpoint better, for example `TypedResults.Problem(result.ErrorMessage, statusCode: StatusCodes.Status400BadRequest)` or a notifier error.

### Storing an Upload in the Media Library

`MediaFileStoreExtensions` (namespace `OrchardCore.Media`, package `OrchardCore.Media.Abstractions`) wraps the whole flow: it calls `CreateAsync()` with `leaveOpen: true`, throws `FileStoreException` when the file is rejected, stores `result.Stream`, and calls `CreatedAsync()`.

```csharp
using Microsoft.AspNetCore.Http;
using OrchardCore.FileStorage;
using OrchardCore.Media;

namespace MyModule.Services;

public sealed class AvatarUploadService
{
    private readonly IMediaFileStore _mediaFileStore;
    private readonly FileCreationService _fileCreationService;

    public AvatarUploadService(
        IMediaFileStore mediaFileStore,
        FileCreationService fileCreationService)
    {
        _mediaFileStore = mediaFileStore;
        _fileCreationService = fileCreationService;
    }

    public async Task<string> UploadAsync(IFormFile file, string userId, CancellationToken cancellationToken)
    {
        // IFileStore.Combine() is an extension in OrchardCore.FileStorage.
        var path = _mediaFileStore.Combine("avatars", userId, Path.GetFileName(file.FileName));

        await using var stream = file.OpenReadStream();

        // Throws FileStoreException (or AntivirusScanningException) when a handler rejects the file.
        return await _mediaFileStore.CreateFileFromStreamAsync(
            _fileCreationService,
            path,
            stream,
            overwrite: true,
            length: file.Length,
            contentType: file.ContentType,
            cancellationToken: cancellationToken);
    }
}
```

Do not wrap this overload in another `CreateAsync()` call; that runs every handler twice.

### Stream Ownership

- By default `CreateAsync()` owns the original stream: the returned result disposes the final stream when it is disposed. Disposing your own `await using` copy as well is harmless.
- Pass `leaveOpen: true` when the caller must keep ownership of the original stream, for example when it reuses or disposes it in a loop. A replacement stream created by a handler is still owned and disposed by the result.
- When a handler replaces the stream, the service disposes the superseded intermediate stream (and the original one unless `leaveOpen` is `true`).
- When a handler throws, the service disposes the streams it owns before rethrowing.

### Writing a File Validation Handler

Return `FileCreatingResult.Failed(stream, ...)` to reject, and `FileCreatingResult.Success(stream)` to continue with the same or a replacement stream. Use `ResultError` (namespace `OrchardCore.Infrastructure`) with a `LocalizedString` message; it becomes `ErrorMessage`.

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.FileStorage;
using OrchardCore.Infrastructure;

namespace MyModule.Services;

public sealed class DocumentTypeFileEventHandler : IFileEventHandler
{
    private static readonly HashSet<string> s_blockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".msi", ".js",
    };

    private static readonly byte[] s_pdfSignature = "%PDF-"u8.ToArray();

    private readonly IStringLocalizer S;

    public DocumentTypeFileEventHandler(IStringLocalizer<DocumentTypeFileEventHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public async Task<FileCreatingResult> CreatingAsync(
        FileCreatingContext context,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(context.FileName);

        if (s_blockedExtensions.Contains(extension))
        {
            return Reject(stream, S["Files of type {0} are not allowed.", extension]);
        }

        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            if (context.ContentType is not null && context.ContentType != "application/pdf")
            {
                return Reject(stream, S["The file {0} is not a PDF document.", context.FileName]);
            }

            // Sniff the signature only when the stream can be rewound for the next handler.
            if (stream.CanSeek)
            {
                var header = new byte[s_pdfSignature.Length];
                stream.Position = 0;
                var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
                stream.Position = 0;

                if (read < header.Length || !header.AsSpan().SequenceEqual(s_pdfSignature))
                {
                    return Reject(stream, S["The file {0} is not a PDF document.", context.FileName]);
                }
            }
        }

        return FileCreatingResult.Success(stream);
    }

    public Task CreatedAsync(IFileStoreEntry fileInfo, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private static FileCreatingResult Reject(Stream stream, LocalizedString message)
        => FileCreatingResult.Failed(stream, new ResultError { Message = message });
}
```

Register handlers with `TryAddEnumerable`, so they are never added twice:

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
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFileEventHandler, DocumentTypeFileEventHandler>());
    }
}
```

Handler rules:

- Use `Singleton` for stateless handlers (the ClamAV handler is a singleton) and `Scoped` when the handler needs scoped services such as `ISiteService` or a localizer bound to the request culture.
- A handler that reads the stream must leave it readable from the start for the next handler and for storage: reset `Position` on a seekable stream, or copy a non-seekable stream to a temporary file from `ITempDirectoryProvider` (open it with `FileOptions.DeleteOnClose`) and return `Success(tempStream)`. See `orchardcore-temp-file-storage`.
- A handler may change `context.Path`, `Length` or `ContentType`, but the caller decides the storage path; built-in callers keep the path they passed in.
- Use `CreatedAsync()` for post-storage work such as auditing or indexing. It receives the stored `IFileStoreEntry`.
- Throw a `FileStoreException` when the handler cannot reach a decision (for example a scanner outage); callers treat it as a rejection.

### Related Skills

- `orchardcore-antivirus` covers the `OrchardCore.Antivirus.ClamAV` handler and its configuration.
- `orchardcore-temp-file-storage` covers `ITempDirectoryProvider`; temporary files are not scanned, so run uploads through this pipeline before storing them permanently.
- `orchardcore-media` covers `MediaOptions` extension and size limits, `IMediaCreatingEventHandler` and the Media API.
- `orchardcore-deployments`, `orchardcore-deployment-remote` and `orchardcore-xml-rpc` cover those upload surfaces.

### Upgrading from 3.x

- The pipeline is new in 4.0. Custom code that wrote uploads directly to `IFileStore`, disk or blob storage should now call `FileCreationService.CreateAsync()` first and `CreatedAsync()` after storing.
- Before: `await mediaFileStore.CreateFileFromStreamAsync(path, stream)` for a user upload. After: `await mediaFileStore.CreateFileFromStreamAsync(fileCreationService, path, stream, length: file.Length, contentType: file.ContentType)`.
- `IMediaCreatingEventHandler` is unchanged and still runs inside `IMediaFileStore`; use `IFileEventHandler` for checks that must cover every upload surface.

### Troubleshooting

| Symptom | Check |
|---|---|
| Custom upload is not checked | The code writes directly to storage or calls the plain `IMediaFileStore.CreateFileFromStreamAsync(path, stream)`; route it through `CreateAsync()` or the overload that takes a `FileCreationService`. |
| Files are checked twice | Remove the extra `CreateAsync()` around the media overload. |
| `Unable to resolve service for type FileCreationService` | No enabled feature registers it; add `services.TryAddTransient<FileCreationService>()`. |
| `ObjectDisposedException` when storing | The `FileCreatingResult` was disposed before `result.Stream` was stored; keep it in scope with `await using`. |
| Stored file is empty or truncated | A handler read the stream without rewinding it. |
| `InvalidOperationException` mentioning a null `FileCreatingResult` | A handler returned `null`; return `Success(stream)` instead. |
