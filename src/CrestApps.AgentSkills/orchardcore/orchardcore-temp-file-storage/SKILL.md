---
name: orchardcore-temp-file-storage
description: Skill for configuring and using Orchard Core 4.0 temporary file storage. Covers ITempDirectoryProvider, DefaultTempDirectoryProvider, the OrchardCore TempDirectory Path setting, tenant-scoped temporary directories, mounting Azure Files, AWS EFS or FSx and SMB or NFS shares, multi-instance TUS resumable uploads, ITusTempStore blob and S3 stores, consuming GetRootDirectory, GetTempFileName and CreateTempSubdirectory from module code, and replacing the default provider. Use this skill when requests mention Orchard Core temporary files, temp directory, Path.GetTempPath, disk full during uploads, TUS uploads across instances, MediaOptions TusTempPath, chunked uploads, deployment import temp files, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.FileStorage, ITempDirectoryProvider, DefaultTempDirectoryProvider, TempDirectoryOptions, OrchardCore.Media.Tus, and ITusTempStore.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Temporary File Storage - Prompt Templates

## Relocate and Use Temporary File Storage

You are an Orchard Core expert. Generate configuration and code for Orchard Core's shared temporary file storage.

### Guidelines

- Orchard Core 4.0 writes temporary files through `ITempDirectoryProvider` (namespace `OrchardCore.FileStorage`): media chunked uploads, TUS resumable uploads, deployment and recipe import/export archives, and tenant recipe uploads.
- The default `DefaultTempDirectoryProvider` stores files under `{Path}/{TenantName}/...`. The tenant sub-directory is added automatically.
- Configure the base path with `OrchardCore:TempDirectory:Path`. When it is omitted, `Path.GetTempPath()` is used, which matches the previous behavior.
- On Unix-like systems, new tenant directories get owner-only permissions (`700`).
- Point `Path` at a mounted file share (local disk, SAN/NAS, Azure Files over SMB, AWS EFS/FSx over NFS), never at object storage. Consumers need seekable local files (`Seek`/`SetLength`, `ZipFile.ExtractToDirectory`, `PhysicalFileProvider`).
- TUS partial uploads go to a `TusUploads` sub-directory of the same location. `MediaOptions.TusTempPath` was removed.
- Media cleanup such as the `TemporaryFileLifetime` purge runs against the configured path.
- Module code that writes temporary files must inject `ITempDirectoryProvider` instead of calling `Path.GetTempPath()`.
- Temporary storage does not scan files. Run user uploads through `FileCreationService` (see `orchardcore-antivirus`) before storing them permanently.
- All C# classes must use the `sealed` modifier, except View Models.

### Configuration

```json
{
  "OrchardCore": {
    "TempDirectory": {
      "Path": "/mnt/shared/octemp"
    }
  }
}
```

```text
OrchardCore__TempDirectory__Path=/mnt/shared/octemp
```

The setting is global; every tenant gets its own sub-directory beneath it.

### Mounting a File Share

Mount the share at the operating system or platform level, then set `Path` to the mount point.

```bash
# Linux or containers, Azure Files over SMB
sudo mkdir -p /mnt/octemp
sudo mount -t cifs //<account>.file.core.windows.net/<share> /mnt/octemp \
  -o vers=3.0,username=<account>,password=<storage-key>,dir_mode=0777,file_mode=0777,serverino,nosharesock,actimeo=30
```

```powershell
# Windows
New-SmbGlobalMapping -RemotePath "\\<account>.file.core.windows.net\<share>" -Credential $cred -Persistent $true -LocalPath Z:
```

```json
{ "OrchardCore": { "TempDirectory": { "Path": "Z:\\octemp" } } }
```

- Azure App Service: **Configuration → Path mappings → Azure Storage Mounts**. Azure Container Apps: an `AzureFile` volume. Set `Path` to the mount path, for example `/mounts/octemp`.
- Make the mount writable by the application identity and keep it in the same region as the app.

### Multi-Instance TUS Uploads

With `OrchardCore.Media.Tus`, every chunk of an upload must reach storage that all instances share. Choose one:

1. Set `OrchardCore:TempDirectory:Path` to a shared file share.
2. Enable sticky sessions (ARR affinity, ALB stickiness, HAProxy or NGINX sticky cookies).
3. Enable Azure Media Storage (`OrchardCore.Media.Azure.Storage`) or Amazon Media Storage (`OrchardCore.Media.AmazonS3`) together with `OrchardCore.Media.Tus`. Their startups replace `ITusTempStore` with `AzureBlobTusTempStore` or `S3TusTempStore`, so TUS partials go to object storage. Other temporary files still use `Path`.

Docker Compose with a shared volume:

```yaml
services:
  cms:
    image: my-orchard-site
    environment:
      OrchardCore__TempDirectory__Path: /app/data/temp
    volumes:
      - octemp:/app/data/temp

volumes:
  octemp:
    # Use an NFS, Azure Files, or EFS backed volume driver for multiple instances.
```

### Using ITempDirectoryProvider in a Module

| Member | Returns |
|---|---|
| `GetRootDirectory()` | The tenant-scoped root directory, created on demand. Use it as a base for tools that need a directory. |
| `GetTempFileName(string extension = null)` | A unique file path (not created). The extension may include or omit the leading dot. |
| `CreateTempSubdirectory(string prefix = null)` | A newly created unique sub-directory, for extracting archives or a `PhysicalFileProvider`. |

```csharp
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using OrchardCore.FileStorage;

namespace MyModule.Services;

public sealed class PackageImportService
{
    private readonly ITempDirectoryProvider _tempDirectoryProvider;

    public PackageImportService(ITempDirectoryProvider tempDirectoryProvider)
    {
        _tempDirectoryProvider = tempDirectoryProvider;
    }

    public async Task ImportAsync(IFormFile package, CancellationToken cancellationToken)
    {
        var archivePath = _tempDirectoryProvider.GetTempFileName(Path.GetExtension(package.FileName));
        var extractPath = _tempDirectoryProvider.CreateTempSubdirectory("import-");

        try
        {
            await using (var stream = File.Create(archivePath))
            {
                await package.CopyToAsync(stream, cancellationToken);
            }

            ZipFile.ExtractToDirectory(archivePath, extractPath);

            // Process the extracted files.
        }
        finally
        {
            File.Delete(archivePath);
            Directory.Delete(extractPath, recursive: true);
        }
    }
}
```

The provider only hands out paths. Create, write, and delete the files yourself; nothing tracks or expires them.

### Replacing the Provider

`DefaultTempDirectoryProvider` is registered per tenant with `TryAddSingleton`. A replacement must still return real, seekable local filesystem paths that support directory enumeration, so there is no blob or S3 implementation.

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
        // ScratchVolumeTempDirectoryProvider is your own ITempDirectoryProvider implementation.
        services.Replace(ServiceDescriptor.Singleton<ITempDirectoryProvider, ScratchVolumeTempDirectoryProvider>());
    }
}
```

### Upgrading from 3.x

- Before: temporary files were written to `Path.GetTempPath()`, and TUS used `MediaOptions.TusTempPath`.
- After: set `OrchardCore:TempDirectory:Path` once for all consumers and remove any `TusTempPath` configuration. TUS partials move to `{Path}/{TenantName}/TusUploads`.
- Replace `Path.GetTempPath()` and `Path.GetTempFileName()` calls in custom modules with `ITempDirectoryProvider`.

### Troubleshooting

| Symptom | Check |
|---|---|
| Disk full during concurrent large uploads | Move `Path` to a larger volume. |
| TUS upload fails when load-balanced | Use a shared `Path`, sticky sessions, or a blob/S3 `ITusTempStore`. |
| Deployment import fails on a blob-mounted path | Object storage cannot provide seekable files; use Azure Files or EFS/FSx. |
| Permission denied creating temp files | Grant the app identity write access to the mount. |
