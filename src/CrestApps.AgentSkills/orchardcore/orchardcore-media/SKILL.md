---
name: orchardcore-media
description: Skill for managing media in Orchard Core. Covers the OrchardCore Media configuration section, MediaOptions, allowed and restricted file extensions, UploadRestrictedMedia, tenant-contained AssetsPath, temporary upload storage, on-demand image resizing with the NetVips engine, custom IImageProcessingEngine and IResizedImageCache, media profiles, Liquid filters, Razor helpers and tag helpers, MediaField editors (Standard, Gallery, Attached), Secure Media, MediaPermissions, the media and MediaProfiles recipe steps, move-attached-media-fields, media indexing, media events, and upgrading from ImageSharp and OrchardCore.Media.ImageSharpV3. Use this skill when requests mention Orchard Core Media, media library configuration, image resizing, resize_url, asset_url, media profiles, MediaField, Secure Media, media permissions, IMediaEventHandler, IMediaCreatingEventHandler, MediaPermittedStorageContext, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Media

## Guidelines

- `OrchardCore.Media` provides the media library, the `IMediaFileStore`, on-demand image resizing, media profiles and `MediaField`. Files are stored in `App_Data/Sites/{tenant}/Media` and served from `/media`.
- The admin UI is the Vue 3 Media Gallery backed by the `api/media` Media API; see `orchardcore-media-gallery` for the gallery, the API, TUS uploads, SignalR and multi-instance setup.
- Only one storage provider is active at a time: file system (default), Azure Blob (`orchardcore-azure-media`) or Amazon S3 (`orchardcore-amazon-s3-media`).
- Images are resized by the built-in middleware and the NetVips engine (`IImageProcessingEngine`). Orchard Core no longer depends on ImageSharp.
- Use `resize_url`, `AssetUrl` or the `img-*` tag helper attributes instead of hand-written query strings: with `UseTokenizedQueryString` (default) the parameters are HMAC-signed per tenant.
- Reference permissions through `OrchardCore.Media.MediaPermissions`.

## Features

| Feature ID | Purpose |
|---|---|
| `OrchardCore.Media` | Media library, resizing, profiles, `MediaField`, Media API. |
| `OrchardCore.Media.Cache` | Local cache of remote (Azure/S3) files; enabled by the cloud storage features. |
| `OrchardCore.Media.Security` | Secure Media, folder-level view permissions. |
| `OrchardCore.Media.Slugify` | SEO-friendly file and folder names (`orchardcore-media-slugify`). |
| `OrchardCore.Media.Tus` | Resumable TUS uploads. |
| `OrchardCore.Media.SignalR` | Real-time gallery updates. |
| `OrchardCore.Media.Indexing.Text`, `.Pdf`, `.OpenXML` | Extract `.txt`/`.md`, `.pdf`, `.docx`/`.pptx` text from files referenced by media fields. |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Media",
        "OrchardCore.Media.Indexing.Pdf"
      ],
      "disable": []
    }
  ]
}
```

## Configuration (`OrchardCore:Media`)

```json
{
  "OrchardCore": {
    "Media": {
      "MaxFileSize": 30000000,
      "CdnBaseUrl": "https://cdn.example.com",
      "AssetsRequestPath": "/media",
      "AssetsPath": "Media",
      "UseTokenizedQueryString": true,
      "SupportedSizes": [ 16, 32, 50, 100, 160, 240, 480, 600, 1024, 2048 ],
      "MaxBrowserCacheDays": 30,
      "MaxSecureFilesBrowserCacheDays": 0,
      "MaxCacheDays": 365,
      "AllowedFileExtensions": [ ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".docx", ".mp4" ],
      "RestrictedFileExtensions": [ ".css", ".js", ".svg" ],
      "ContentSecurityPolicy": "default-src 'self'; style-src 'unsafe-inline'",
      "MaxUploadChunkSize": 104857600,
      "TemporaryFileLifetime": "01:00:00",
      "DisableThumbnails": false
    }
  }
}
```

- Environment variable form: `OrchardCore__Media__MaxFileSize=50000000`.
- `AllowedFileExtensions` and `RestrictedFileExtensions` are case-insensitive, must not overlap (tenant startup fails), and **replace** the defaults, so list every extension you keep. Extensions in neither list are rejected.
- Extensions in `RestrictedFileExtensions` (default `.css`, `.js`, `.svg`) also need the security-critical `UploadRestrictedMedia` permission (Administrator and Editor by default). The check covers regular, API, TUS, copy/rename and remote publishing uploads. Recipe imports may use either list.
- `MaxFileSize` is enforced client-side in the gallery, pickers and editors, and on the server. Also raise Kestrel/IIS/form limits (see below).
- `AssetsPath` is relative to the tenant data folder. Absolute paths, drive prefixes (including `C:Media`), empty segments and segments ending in a dot or space (`.`, `..`) fail options validation. To relocate tenant data, set the host app data location (`ORCHARD_APP_DATA`) instead.
- In-progress chunked and TUS uploads go to `OrchardCore:TempDirectory:Path` (see `orchardcore-temp-file-storage`).
- `CdnBaseUrl` prefixes generated media URLs; query strings on the asset path (such as `?v=` or resize parameters) are preserved.
- `DisableThumbnails` forces the gallery list view (see `orchardcore-media-gallery`).
- Fine-tune the media `StaticFileOptions` with `services.PostConfigure<MediaOptions>(o => ...)`; module `wwwroot` files use `services.Configure<StaticFileOptions>(...)`.

### Upload size limits

```csharp
services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = 50_000_000);
services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = 50_000_000);
services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 50_000_000);
```

On IIS also set `<requestLimits maxAllowedContentLength="50000000" />` in
`web.config`.

## Image Processing

- `MediaImageProcessingMiddleware` validates the request, parses it into `ImageProcessingCommands` (`Width`, `Height`, `ResizeMode`, `Format`, `Quality`, `BackgroundColor`, `FocalPointX/Y`, `AutoOrient`) and calls `IImageProcessingEngine.ProcessAsync`.
- The default `VipsImageProcessingEngine` outputs `jpg`, `png`, `gif` and `webp`; any other format falls back to JPEG (`bmp` and `tga` are not supported).
- Native libvips binaries come from `NetVips.Native`. For a runtime it does not cover, add the matching `NetVips.Native.<rid>` package to the web project.
- Resized images are cached through `IResizedImageCache`; the default writes to `wwwroot/{tenant}/media-cache`. Azure and S3 can hold this cache (`OrchardCore.Media.Azure.ImageCache`, `OrchardCore.Media.AmazonS3.ImageCache`).

### Custom image processing engine

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Media.Processing;
using OrchardCore.Modules;

public sealed class MyImageProcessingEngine : IImageProcessingEngine
{
    public async Task<ImageProcessingResult> ProcessAsync(
        Stream input,
        ImageProcessingCommands commands,
        CancellationToken cancellationToken = default)
    {
        var output = new MemoryStream();

        // Resize "input" with your imaging library using commands.Width, commands.Height,
        // commands.ResizeMode, commands.Quality, commands.BackgroundColor and the focal point.
        await input.CopyToAsync(output, cancellationToken);
        output.Position = 0;

        return new ImageProcessingResult
        {
            Output = output,
            ContentType = "image/jpeg",
        };
    }
}

[RequireFeatures("OrchardCore.Media")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IImageProcessingEngine, MyImageProcessingEngine>());
    }
}
```

A custom `IResizedImageCache` implements `GetAsync(cacheKey, fileExtension)`,
`SetAsync`, `ClearAsync` and `ClearStaleAsync`, and is registered the same way.

## Media Profiles

Create profiles under **Media → Profiles** (`ManageMediaProfiles`) or with the
`MediaProfiles` step. `Mode` is `Max`, `Crop`, `Pad`, `BoxPad`, `Min` or
`Stretch`; `Format` is `Undefined`, `Jpg`, `Png`, `Gif` or `WebP`.

```json
{
  "steps": [
    {
      "name": "MediaProfiles",
      "MediaProfiles": {
        "thumbnail": {
          "Hint": "Square thumbnail for listings",
          "Width": 240,
          "Height": 240,
          "Mode": "Crop",
          "Format": "WebP",
          "Quality": 80,
          "BackgroundColor": "",
          "AutoOrient": true
        }
      }
    }
  ]
}
```

Profiles are exported with the **All Media Profiles** deployment step.

## Rendering Media

### Liquid

```liquid
{{ Model.ContentItem.Content.Article.Image.Paths.first | asset_url | img_tag: alt: 'Cover' }}
{{ 'animals/kittens.jpg' | asset_url | resize_url: width: 400, height: 300, mode: 'crop' | img_tag }}
{{ 'animals/kittens.jpg' | asset_url | resize_url: width: 800, format: 'webp', quality: 75 }}
{{ 'animals/kittens.jpg' | asset_url | resize_url: profile: 'thumbnail' }}
{{ 'animals/kittens.jpg' | asset_url | append_version }}

{% assign anchor = Model.ContentItem.Content.Article.Image.Anchors.first %}
{{ 'animals/kittens.jpg' | asset_url | resize_url: width: 100, height: 240, mode: 'crop', anchor: anchor }}
```

Named and positional `resize_url` arguments cannot be mixed. `bgcolor` applies
to `pad`/`boxpad`.

### Razor

Reference the `OrchardCore.Media` package from the theme or module and add
`@addTagHelper *, OrchardCore.Media` to `_ViewImports.cshtml`.

```razor
<img src="@Orchard.AssetUrl(Model.Paths[0], width: 400, height: 300, resizeMode: ResizeMode.Crop)" alt="" />
<img src="@await Orchard.AssetProfileUrlAsync(Model.Paths[0], "thumbnail")" alt="" />
<img asset-src="@Model.Paths[0]" alt="@Model.MediaTexts[0]" img-width="800" img-format="WebP" img-quality="75" asp-append-version="true" />
<img asset-src="@Model.Paths[0]" img-profile="thumbnail" img-anchor="@Model.Field.GetAnchors()[0]" alt="" />
<a asset-href="docs/brochure.pdf" target="_blank">Download</a>
```

`MediaTexts[]` and `Anchors[]` are index-aligned with `Paths[]`.

## Media Fields

`MediaFieldSettings`: `Multiple`, `AllowMediaText`, `AllowAnchors`,
`AllowedExtensions` (can only narrow the global lists), plus `Hint`/`Required`.

| Editor | Behavior |
|---|---|
| Standard (default) | Picks existing library files; Secure Media folder permissions apply. |
| `Gallery` | Same storage as Standard, with card/list views and drag-and-drop reordering. |
| `Attached` | Uploads into `mediafields/{ContentType}/{ContentItemId}/` with hash-based names; requires `ManageAttachedMediaFieldsFolder`; with Secure Media, files follow the item's `ViewContent`. Supports TUS. |

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("Article", part => part
    .WithField("Attachments", field => field
        .OfType("MediaField")
        .WithDisplayName("Attachments")
        .WithEditor("Attached")
        .WithSettings(new MediaFieldSettings
        {
            Multiple = true,
            AllowMediaText = true,
            AllowedExtensions = [".pdf", ".docx"],
        })));
```

After switching existing fields to `Attached`, move the referenced files:

```json
{
  "steps": [
    {
      "name": "move-attached-media-fields",
      "ContentTypes": [ "Article", "Report" ]
    }
  ]
}
```

Omit `ContentTypes` to process every type with an Attached media field. The
step runs in a background job and saves in batches of 50.

## Importing Files (`media` step)

```json
{
  "steps": [
    {
      "name": "media",
      "Files": [
        { "TargetPath": "images/logo.png", "SourcePath": "../wwwroot/images/logo.png" },
        { "TargetPath": "images/banner.jpg", "SourceUrl": "https://example.com/banner.jpg" },
        { "TargetPath": "docs/readme.txt", "Base64": "SGVsbG8=" }
      ]
    }
  ]
}
```

`TargetPath` (alias `Path`) must be a relative path inside the tenant media
store; rooted paths, drive prefixes, empty segments and `.`/`..` segments are
rejected before anything is read or overwritten.

## Permissions

| `MediaPermissions` member | Name | Notes |
|---|---|---|
| `ManageMediaFolder` | `ManageMediaFolder` | Broadest; implies all others below it and the Secure Media view permissions. |
| `ManageOthersMedia` | `ManageOthersMediaContent` | Implied by `ManageMediaFolder`. |
| `ManageOwnMedia` | `ManageOwnMediaContent` | Implied by `ManageOthersMedia`. |
| `ManageAttachedMediaFieldsFolder` | `ManageAttachedMediaFieldsFolder` | `mediafields/` folder. |
| `ManageMedia` | `ManageMediaContent` | Minimum to open the library. |
| `UploadRestrictedMedia` | `UploadRestrictedMedia` | Restricted extensions. |
| `ManageMediaProfiles` | `ManageMediaProfiles` | Profiles. |
| `ViewMediaOptions` | `ViewMediaOptions` | **Settings → Media → Options** page. |
| `ManageMediaApiSettings` | `ManageMediaApiSettings` | Media API authentication scheme. |
| `ManageAssetCache` | `ManageAssetCache` | Remote media cache purge. |
| `ViewMedia`, `ViewRootMedia`, `ViewOthersMedia`, `ViewOwnMedia` | `ViewMediaContent`, `ViewRootMediaContent`, `ViewOthersMediaContent`, `ViewOwnMediaContent` | Only with Secure Media. |

```csharp
if (!await _authorizationService.AuthorizeAsync(User, MediaPermissions.ManageMediaFolder, (object)"images/products"))
{
    return Forbid();
}
```

### Secure Media (`OrchardCore.Media.Security`)

- Adds `ViewMediaContent_{folder}` permissions for the root and each first-level folder, plus own/others view permissions on top of `ManageMediaContent`.
- Unauthorized and anonymous requests get `404`. Secure files are sent with `Cache-Control: no-store` unless `MaxSecureFilesBrowserCacheDays` is set.
- Enabling it changes access immediately: only Administrators view all media, and **Authenticated** gets `ViewOwnMediaContent`. The **Anonymous** role has no view permission by default, so grant `ViewMediaContent` (or folder permissions) to Anonymous for public media. An admin notification reminds you to review roles.

## Media Events

```csharp
using OrchardCore.Media.Events;

public sealed class MediaQuotaEventHandler : IMediaEventHandler
{
    public Task MediaPermittedStorageAsync(MediaPermittedStorageContext context)
    {
        context.Constrain(10L * 1024 * 1024 * 1024); // smallest limit wins
        return Task.CompletedTask;
    }

    public Task MediaCreatedFileAsync(MediaCreatedContext context)
    {
        // React to uploads, e.g. audit or notify.
        return Task.CompletedTask;
    }
}

public sealed class StripMetadataHandler : IMediaCreatingEventHandler
{
    public Task<Stream> MediaCreatingAsync(MediaCreatingContext context, Stream creatingStream)
        => Task.FromResult(creatingStream); // return a transformed stream to replace the upload
}
```

Register with `services.AddScoped<IMediaEventHandler, MediaQuotaEventHandler>()`
and `services.AddScoped<IMediaCreatingEventHandler, StripMetadataHandler>()`.
`IMediaEventHandler` has default implementations for every method (deleting,
moving, copying, directory events). Uploads through `IMediaFileStore` also run
the shared `IFileEventHandler` pipeline (`FileCreationService`), which
`orchardcore-antivirus` uses to scan files.

## Media Indexing

Tick **Include this element in the index** on a media field; the index then gets
`{Part}.{Field}.MediaText` and, with an extraction feature, `{Part}.{Field}.FileText`.
Standalone library files that no media field references are not indexed.

## Upgrading from 3.x

- `OrchardCore.Media.ImageSharpV3` and the **Media ImageSharp Image Processing** feature were removed. Remove the package reference and the feature from recipes; NetVips processes images (tenants that had it enabled keep working). To keep ImageSharp, implement `IImageProcessingEngine` in your project and replace the registration as shown above.
- `OrchardCore.Media.Permissions`, `SecureMediaPermissions.View*` and `MediaCachePermissions.ManageAssetCache` members were removed; use `MediaPermissions.*` (role claim names are unchanged).
- The Anonymous role no longer gets `ViewMediaContent` from Secure Media; grant it explicitly if needed.
- `.css`, `.js` and `.svg` moved to `RestrictedFileExtensions`; grant `UploadRestrictedMedia` to roles that upload them.
- `MediaOptions.TusTempPath` was removed; use `OrchardCore:TempDirectory:Path`.
- Rename `OrchardCore_Media` to `Media` in configuration (env var `OrchardCore__Media__...`); the old name is deprecated but still read.
- The Media `AdminController` AJAX endpoints were replaced by the `api/media` Media API (`orchardcore-media-gallery`).
- Coming from 2.x: the `is-cache` folder became `media-cache`, and the Azure/S3 `ImageSharpImageCache` feature IDs became `ImageCache`.
