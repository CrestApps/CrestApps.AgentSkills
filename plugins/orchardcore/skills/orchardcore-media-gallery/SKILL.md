---
name: orchardcore-media-gallery
description: Skill for the Orchard Core 4 Vue 3 Media Gallery and the api/media Media API. Covers gallery features, list and grid views, DisableThumbnails, upload size and extension limits in pickers, MediaApiSettings AuthenticationScheme Cookie or Bearer, antiforgery, the MediaApiPkce, MediaApiStandalone and MediaApiStandaloneLocalhost recipes, the media_gallery OpenID client, standalone gallery config.json, OrchardCore.Media.Tus resumable uploads, ITusTempStore for Azure Blob and Amazon S3, OrchardCore.Media.SignalR real-time updates with the OrchardCore.SignalR, OrchardCore.SignalR.Redis and OrchardCore.SignalR.Azure backplanes, the /hubs/media hub, multi-instance media deployments, and migrating custom code from the removed Media AdminController AJAX endpoints. Use this skill when requests mention the Orchard Core Media Library admin UI, Media API endpoints, media upload from JavaScript, TUS uploads, media SignalR, ManageMediaApiSettings, or scaling the Media Library across instances.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Media Gallery and Media API

Orchard Core 4.0 rewrote the Media Library admin UI as a **Vue 3 Media Gallery**
backed by the **Media API**, minimal-API endpoints under `api/media`. The same
gallery powers the Media Library page, the media picker modal (media fields,
HTML/Markdown editors) and the media field gallery editor. For media storage,
profiles, permissions and image resizing see `orchardcore-media`.

## Features

| Feature ID | Purpose |
|---|---|
| `OrchardCore.Media` | Gallery, Media API, picker. Always required. |
| `OrchardCore.Media.Tus` | Resumable chunked uploads through the TUS protocol (`/api/media/tus`). |
| `OrchardCore.Media.SignalR` | Real-time updates on the `/hubs/media` hub. Depends on `OrchardCore.SignalR`. |
| `OrchardCore.SignalR` | Shared SignalR hosting, the `signalr` client resource and the `SignalR` policy. |
| `OrchardCore.SignalR.Redis` | Redis backplane. Depends on `OrchardCore.Redis`. |
| `OrchardCore.SignalR.Azure` | Azure SignalR Service backplane. |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Media",
        "OrchardCore.Media.Tus",
        "OrchardCore.Media.SignalR"
      ],
      "disable": []
    }
  ]
}
```

## Gallery Behavior

- **Files**: upload (button or drag-and-drop), rename, move (context menu or drag onto a folder in the tree), copy, delete (with confirmation), download, and **View** (opens the file URL).
- **Folders**: create subfolder, delete folder (not the root), breadcrumb navigation.
- **Search, sort, pagination**: filter by name, sort by name, size, MIME type or last modified; page size in the settings popover.
- **Views**: opens in the **list view** (no thumbnails downloaded); the toggle switches to the **grid view**. View and thumbnail size are persisted per browser in local storage.
- **Storage info** popover: provider name, available space, hierarchical namespace and atomic move support, SignalR and TUS status.
- **Uploads**: up to 5 concurrent requests; extra files are queued.

### Disable thumbnails

Force the list view everywhere (Media Library, media picker modal, media field
gallery editor) and hide the grid toggle and thumbnail size setting:

```json
{
  "OrchardCore": {
    "Media": {
      "DisableThumbnails": true
    }
  }
}
```

### Upload limits

- `Media:MaxFileSize` is enforced client-side (files over a positive limit are rejected before upload, in the gallery, pickers and HTML/Markdown editor integrations), by `MediaSizeLimitAttribute` (Kestrel `MaxRequestBodySize` and form `MultipartBodyLengthLimit`), and by TUS (`MaxAllowedUploadSizeInBytesLong`). The server stays authoritative.
- Behind IIS, set `maxAllowedContentLength` in `web.config` to at least `MaxFileSize`; a startup warning is logged on a mismatch.
- `AllowedFileExtensions` is enforced on both sides. Extensions in `RestrictedFileExtensions` (`.css`, `.js`, `.svg` by default) also require `UploadRestrictedMedia`. The gallery receives the effective list for the current user.
- Media field `AllowedExtensions` only narrow the global list; they cannot make a restricted or disallowed extension uploadable.

## Media API Authentication

The Media API, the TUS endpoint and the media hub authenticate with exactly one
scheme, chosen in **Configuration → Settings → Media → API**
(`MediaApiSettings.AuthenticationScheme`, permission `ManageMediaApiSettings`,
granted to Administrators). Changing it releases the shell so the `MediaApi`
policy is rebuilt.

| Scheme | Behavior |
|---|---|
| `Cookie` (default) | Same-origin admin cookie. Mutating requests must carry the antiforgery token (`RequestVerificationToken` header or `__RequestVerificationToken` form field); failures return `400` Problem Details. Works out of the box. |
| `Bearer` | Only Bearer tokens on the `Api` scheme. The gallery silently gets an OAuth2 authorization-code + PKCE token (hidden iframe, `prompt=none`) from the tenant's OpenID server, renews it, and sends it to API calls, Uppy/TUS uploads and SignalR. A `401` is retried once after renewal. Requires `OrchardCore.OpenId.Validation`. |

The media hub accepts both the application cookie and `Api` bearer tokens in
either mode.

### Provision Bearer mode

Run the **Media API — Bearer/PKCE** recipe (`MediaApiPkce`) from
**Configuration → Recipes**. It enables `OrchardCore.OpenId.Management`,
`OrchardCore.OpenId.Server` and `OrchardCore.OpenId.Validation`, sets
`MediaApiSettings.AuthenticationScheme` to `Bearer`, enables the
authorization-code flow with PKCE required, sets local token validation
(`Tenant: "Default"`), and registers the public `media_gallery` client. Before
running it, replace `https://localhost:5001` with the tenant origin (keep the
`/OrchardCore.Media/media-gallery-oidc-silent.html` path, including any tenant
prefix), use the right tenant name, and make sure gallery users have roles
granting media permissions (the `roles` scope carries them into the token).

Switch only the scheme in your own recipe:

```json
{
  "steps": [
    {
      "name": "Settings",
      "MediaApiSettings": {
        "AuthenticationScheme": "Bearer"
      }
    }
  ]
}
```

### Standalone gallery app

The gallery SPA can run on its own origin. The first sign-in is an interactive
redirect; renewal stays silent.

- Apply `MediaApiPkce` first, then **Media API — Standalone external app** (`MediaApiStandalone`). It enables `OrchardCore.Cors` with a default policy for the app origin (`AllowCredentials: false`), adds `callback.html` and `silent-callback.html` redirect URIs to `media_gallery`, and reloads the tenant. Replace `https://media.example.com` and `https://localhost:5001`; CORS origins have no path or trailing slash.
- `MediaApiStandaloneLocalhost` is the same for local development (app at `http://localhost:5173`, tenant at `https://localhost:5001`). Never use it in production.
- Deploy a `config.json` next to the app's `index.html`:

```json
{
  "orchardBaseUrl": "https://cms.example.com/",
  "appBaseUrl": "https://media.example.com/",
  "oidcClientId": "media_gallery",
  "oidcScope": "openid email profile roles",
  "signalrEnabled": false,
  "maxFileSize": 30000000
}
```

Keep `maxFileSize` equal to the tenant's `Media:MaxFileSize`. Set
`signalrEnabled` to `true` only once CORS covers `/hubs/media`.

## Media API Endpoints

All endpoints require the `MediaApi` policy except `localizations`, and then
check `MediaPermissions` (`ManageMedia`, folder-scoped `ManageMediaFolder`,
`UploadRestrictedMedia`) in the handler. Errors are RFC 9457 Problem Details.
Simple parameters are query string values.

| Method and route | Parameters | Returns |
|---|---|---|
| `GET api/media/GetDirectoryTree` | none | Folder tree (`DirectoryTreeNodeDto`) |
| `GET api/media/GetFolders` | `path`, `skip`, `take` | `PaginatedFoldersDto` (`Items`, `HasMore`) |
| `GET api/media/GetDirectoryContent` | `path`, `extensions` | `DirectoryContentDto` (`Folders`, `Files`) |
| `GET api/media/GetMediaItems` | `path`, `extensions` | Files of a folder |
| `GET api/media/GetAllMediaItems` | `extensions` | Every file |
| `GET api/media/GetMediaItem` | `path` | One `FileStoreEntryDto` |
| `GET api/media/GetMediaFieldItems` | `paths` (repeated) | Items selected in a media field |
| `GET api/media/GetPermittedStorage` | none | `PermittedStorageDto` (`Bytes`, `Text`) |
| `GET api/media/localizations` | none | Gallery strings (anonymous) |
| `POST api/media/Upload` | `path`, `extensions`, multipart files | `UploadFilesResultDto` (per-file `Error`) |
| `POST api/media/CreateFolder` | `path`, `name` | Created folder |
| `POST api/media/DeleteFolder` | `path` | `200` |
| `POST api/media/DeleteMedia` | `path` | `200` |
| `POST api/media/DeleteMediaList` | JSON body, array of paths | `200` |
| `POST api/media/MoveMedia` | `oldPath`, `newPath` (rename or move) | `200` |
| `POST api/media/MoveMediaList` | JSON body `{ mediaNames, sourceFolder, targetFolder }` | `200` |
| `POST api/media/CopyMedia` | `oldPath`, `newPath` | Copied file |
| `GET api/media/TusFileInfo/{uploadId}` | TUS only | Upload info |
| `/api/media/tus` | TUS protocol | Resumable upload endpoint (TUS only) |

`MoveMedia`, `CopyMedia` and uploads re-check the target extension against the
user's effective extension list. See `references/media-api-examples.md` for
calls from JavaScript and C#.

## TUS Resumable Uploads (`OrchardCore.Media.Tus`)

- Replaces the default upload mechanism in the gallery and the Attached media field editor. Files go in chunks of `Media:MaxUploadChunkSize` (100 MB by default; 5 MB when set to `0`), can be paused and resumed from the upload toast, and resume after a network failure through fingerprints in the browser's local storage.
- Incomplete uploads expire after `Media:TemporaryFileLifetime` (sliding).
- Partial data goes through `ITusTempStore`. The default `DiskTusTempStore` writes to the `TusUploads` sub-directory of `OrchardCore:TempDirectory:Path` (tenant-scoped; see `orchardcore-temp-file-storage`).
- With Azure Media Storage or Amazon S3 Media Storage enabled, TUS automatically uses `AzureBlobTusTempStore` (staged block blobs) or `S3TusTempStore` (S3 multipart uploads) under a `_tus-uploads` prefix of the media container or bucket, so any instance can continue an upload.
- Upload metadata uses `IDistributedCache` and chunk locks use `IDistributedLock`, so enable `OrchardCore.Redis.Cache` and `OrchardCore.Redis.Lock` when several instances serve TUS requests.

Replace the store in your own module:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Media.Services;
using OrchardCore.Modules;

[RequireFeatures("OrchardCore.Media.Tus")]
public sealed class TusStoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ITusTempStore, MyTusTempStore>());
    }
}
```

`ITusTempStore` members: `CreateFileAsync`, `AppendDataAsync` (stream and
`PipeReader` overloads, returning bytes written), `DeleteFileAsync`,
`FileExistAsync`, `OpenReadStream`, `GetFileLength`.

## SignalR Real-time Updates (`OrchardCore.Media.SignalR`)

- Maps `MediaHub` at `/hubs/media`. The `MediaHub` policy requires `ManageMedia` and accepts cookie or `Api` bearer authentication; headless clients sending `access_token` need `OrchardCore.OpenId.Validation`.
- Clients call `SubscribePath(path)` and `UnsubscribePath(path)` (folder-authorized) and receive `MediaChanged` messages with `action` values `fileUploaded`, `fileDeleted`, `fileMoved`, `fileCopied`, `directoryCreated` and `directoryDeleted`.
- Without a backplane, updates only reach clients on the same instance.

```json
{
  "OrchardCore": {
    "Redis": {
      "Configuration": "your-redis-host:6379,password=...,ssl=true"
    },
    "SignalR": {
      "Azure": {
        "ConnectionString": "Endpoint=https://<service>.service.signalr.net;AccessKey=...;Version=1.0;",
        "ApplicationName": "OrchardCore"
      }
    }
  }
}
```

- `OrchardCore.SignalR.Redis` reuses `OrchardCore:Redis:Configuration` and isolates each tenant on its own channel prefix.
- `OrchardCore.SignalR.Azure` reads `OrchardCore:SignalR:Azure`. `ApplicationName` is optional (default `OrchardCore`), must start with a letter and contain only letters, numbers and underscores; set a unique value when several deployments share one service. A tenant identifier is appended automatically.
- Enable only one backplane per tenant; enabling both fails tenant startup. A missing connection string logs a warning and falls back to in-memory.
- Your own hubs depend on `OrchardCore.SignalR`, use `routes.MapHub<T>()` and `HubOptions<T>`; server-rendered pages load `<script asp-name="signalr" at="Foot"></script>`.

## Multi-instance Deployment

| Component | Configure |
|---|---|
| SignalR backplane | `OrchardCore.SignalR.Redis` or `OrchardCore.SignalR.Azure` |
| TUS chunks | Shared `OrchardCore:TempDirectory:Path` (NFS, Azure Files, EFS), Azure/S3 media storage (`ITusTempStore`), or sticky sessions |
| TUS metadata and locks | `OrchardCore.Redis.Cache` and `OrchardCore.Redis.Lock` |
| Media files | Azure Blob, Amazon S3, or a shared filesystem (`orchardcore-azure-media`, `orchardcore-amazon-s3-media`) |
| Data Protection keys | `OrchardCore.Redis.DataProtection` or `OrchardCore.DataProtection.Azure` |

Sharing the Data Protection key ring is mandatory once you scale out: the admin
cookie, antiforgery tokens and the OpenID access tokens (Data Protection
format) are all encrypted with it, and per-instance keys surface as random
`401` errors in the gallery. Sticky sessions keep TUS chunks on one instance
but lose the upload if that instance stops. Non-TUS uploads use one request per
file and are unaffected.

A single Redis instance can cover the backplane, keys, cache and locks:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Redis",
        "OrchardCore.Redis.Cache",
        "OrchardCore.Redis.Lock",
        "OrchardCore.Redis.DataProtection",
        "OrchardCore.SignalR.Redis",
        "OrchardCore.Media.SignalR",
        "OrchardCore.Media.Tus"
      ],
      "disable": []
    }
  ]
}
```

## Upgrading from 3.x

- The Media `AdminController` AJAX actions were removed. Map custom scripts and themes to the Media API:
  - `Admin/Media/GetFolders` → `GET api/media/GetFolders` (paginated) or `GET api/media/GetDirectoryTree`
  - `GetMediaItems`, `GetMediaItem` → `GET api/media/GetMediaItems`, `GET api/media/GetMediaItem`
  - `Upload` → `POST api/media/Upload`
  - `DeleteFolder`, `DeleteMedia`, `MoveMedia`, `CreateFolder` → same names under `POST api/media/`
  - `DeleteMediaList` → `POST api/media/DeleteMediaList` with a JSON array body
  - `MoveMediaList` → `POST api/media/MoveMediaList` with a JSON object body
  - `MediaApplication` → removed; the gallery is mounted by the Media Library page and the picker
- In Cookie mode send the antiforgery token on every POST; in Bearer mode send `Authorization: Bearer` instead.
- The Vue 2 media app and its jQuery helpers are gone. Admin Vue apps use Vue 3 and AJAX-injected editors are ES modules.
- `MediaOptions.TusTempPath` was removed; set `OrchardCore:TempDirectory:Path` (TUS uses its `TusUploads` sub-directory).
- Rename the `OrchardCore_Media` configuration section to `Media` (the old name is deprecated but still read).
