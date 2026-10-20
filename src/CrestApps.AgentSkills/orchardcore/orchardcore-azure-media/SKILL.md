---
name: orchardcore-azure-media
description: Skill for configuring Azure Blob media storage in Orchard Core. Covers the OrchardCore.Media.Azure.Storage and OrchardCore.Media.Azure.ImageCache features, the OrchardCore Media Azure and Media Azure ImageCache configuration sections, environment variable and Key Vault secret names, connection and container setup, tenant templating with ShellSettings, Azure Data Lake Storage Gen2 hierarchical namespace support with UseHierarchicalNamespace, the Media Cache and purging, resized image caching in Blob Storage, TUS resumable upload staging in Blob Storage, and CDN use. Use this skill for Azure Media Storage, MediaBlobStorageOptions, MediaBlobImageCacheOptions, BlobFileStore, ContainerName, BasePath, CreateContainer, RemoveContainer, RemoveFilesFromBasePath, AzureResizedImageCache, ADLS Gen2, upgrading from OrchardCore_Media_Azure or OrchardCore.Media.Azure.ImageSharpImageCache, or related implementation, setup, extension, and troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure Media Storage

## Module Overview

The `OrchardCore.Media.Azure` module stores media in Azure Blob Storage instead
of `App_Data`:

- **`OrchardCore.Media.Azure.Storage`** — replaces the media store with Blob Storage. Depends on (and enables) `OrchardCore.Media.Cache`.
- **`OrchardCore.Media.Azure.ImageCache`** — stores resized images in Blob Storage instead of `wwwroot/{tenant}/media-cache`.

Media is still served by the Orchard Core site: the Media Cache fetches blobs on
demand, which keeps on-the-fly resizing working, and `AssetUrl` helpers point to
the site (or to `Media:CdnBaseUrl`). Only one storage provider is active at a
time.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Media.Azure.Storage",
        "OrchardCore.Media.Azure.ImageCache"
      ],
      "disable": []
    }
  ]
}
```

## Azure Media Storage (`OrchardCore:Media:Azure`)

| Property | Description | Default |
|---|---|---|
| `ConnectionString` | Storage account connection string (required). | |
| `ContainerName` | Container name, a valid lowercase DNS name. Templatable. | |
| `BasePath` | Optional sub-path inside the container. Templatable. | |
| `CreateContainer` | Create the container on tenant activation if missing. | `true` |
| `RemoveContainer` | Delete the container when the tenant is removed. | `false` |
| `UseHierarchicalNamespace` | Force Gen2 (`true`) or Gen1 (`false`) behavior. Leave unset to auto-detect. | unset |

```json
{
  "OrchardCore": {
    "Media": {
      "Azure": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
        "ContainerName": "media",
        "BasePath": "Media",
        "CreateContainer": true,
        "RemoveContainer": false
      }
    }
  }
}
```

- Environment variables: `OrchardCore__Media__Azure__ConnectionString`, `OrchardCore__Media__Azure__ContainerName`.
- Azure Key Vault secret names (see `orchardcore-azure-key-vault`): `OrchardCore--Media--Azure--ConnectionString`.
- With a missing or invalid connection string the feature does not activate and an error is logged; media falls back to the file system.
- Set `CreateContainer` to `false` when the container is pre-provisioned to skip the startup check.

### Hierarchical namespace (ADLS Gen2)

- `BlobFileStore` (Azure media and Azure Shells configuration) supports flat (Gen1) and hierarchical namespace (HNS / ADLS Gen2) accounts.
- The store probes the account with `GetAccountInfo` (also during tenant activation). With HNS, directory create/delete and file moves use native Data Lake APIs: atomic moves and real directories. Gen1 keeps the marker-file directory emulation.
- Auto-detection needs account-level permissions and fails with a container-scoped SAS token; then set `UseHierarchicalNamespace` explicitly.
- The DFS endpoint is derived from the connection string; no extra setting.
- The gallery storage info popover reports hierarchical namespace and atomic move support.

```json
{
  "OrchardCore": {
    "Media": {
      "Azure": {
        "ConnectionString": "BlobEndpoint=https://myaccount.blob.core.windows.net/;SharedAccessSignature=sv=...",
        "ContainerName": "media",
        "CreateContainer": false,
        "UseHierarchicalNamespace": true
      }
    }
  }
}
```

## Tenant Templating

- `ContainerName` and `BasePath` accept Liquid with `ShellSettings` in scope. `{{ ShellSettings.Name }}` is lowercased; the full name must still follow Azure naming rules.
- Only default Liquid filters and tags are available (no `slugify`).

```json
{
  "OrchardCore": {
    "Media": {
      "Azure": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
        "ContainerName": "{{ ShellSettings.Name }}-media",
        "BasePath": "Media",
        "CreateContainer": true
      }
    }
  }
}
```

Single container with a folder per tenant:

```json
{
  "OrchardCore": {
    "Media": {
      "Azure": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
        "ContainerName": "sharedmedia",
        "BasePath": "{{ ShellSettings.Name }}/Media",
        "CreateContainer": true
      }
    }
  }
}
```

## Media Cache

- Enabled automatically with Azure Media Storage. Files fetched from Blob Storage are cached locally so they can be resized and served.
- **Settings → Media → Cache** (permission `MediaPermissions.ManageAssetCache`) offers a **Purge** action. Local remote-media files live in `wwwroot/{tenant}/ms-cache`.
- When fronted by a CDN, purge only after the CDN has fetched most assets. Each CDN PoP caches independently and CDNs evict on their own schedule, so the Media Cache must always be able to re-fetch from the container.

## Azure Media Image Cache (`OrchardCore:Media:Azure:ImageCache`)

Stores resized images in Blob Storage (`AzureBlobResizedImageCache`, options
`MediaBlobImageCacheOptions`). Useful with ephemeral file systems (containers,
clean deployments) and throttled local disks (App Service).

| Property | Description | Default |
|---|---|---|
| `ConnectionString` | Storage account connection string (required). | |
| `ContainerName` | Cache container. Templatable. | |
| `BasePath` | Sub-path inside the container. Templatable. | |
| `CreateContainer` | Create the container if missing. | `true` |
| `RemoveContainer` | Delete the container when the tenant is removed. | `false` |
| `RemoveFilesFromBasePath` | Delete files under `BasePath` on tenant removal when `RemoveContainer` is `false`. | `false` |

```json
{
  "OrchardCore": {
    "Media": {
      "Azure": {
        "ImageCache": {
          "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
          "ContainerName": "imagecache",
          "BasePath": "{{ ShellSettings.Name }}",
          "CreateContainer": true,
          "RemoveContainer": false,
          "RemoveFilesFromBasePath": true
        }
      }
    }
  }
}
```

- Environment variable: `OrchardCore__Media__Azure__ImageCache__ConnectionString`.
- Cache files are only removed with the tenant when each tenant has its own container, or with `RemoveFilesFromBasePath`.
- The feature replaces `IResizedImageCache` only when `ConnectionString` and `ContainerName` are valid; otherwise the local `media-cache` folder stays in use and an error is logged.

## Resumable Uploads

When `OrchardCore.Media.Tus` is also enabled, TUS partial uploads are staged as
uncommitted blocks (`AzureBlobTusTempStore`) under a `_tus-uploads` prefix of
the media container, so any instance can continue an upload without a shared
temp folder. See `orchardcore-media-gallery` for TUS, SignalR and
multi-instance guidance.

## Upgrading from 3.x

- Rename `OrchardCore_Media_Azure` to `Media:Azure` and `OrchardCore_Media_Azure_ImageSharp_Cache` to `Media:Azure:ImageCache`, including env vars (`OrchardCore__OrchardCore_Media_Azure__ConnectionString` → `OrchardCore__Media__Azure__ConnectionString`) and Key Vault secrets. The old names are deprecated but still read; the new name wins when both exist.
- Use the feature ID `OrchardCore.Media.Azure.ImageCache` in recipes. The obsolete `OrchardCore.Media.Azure.ImageSharpImageCache` ID only enables the new feature.
- `ImageSharpBlobImageCacheOptions` is now `MediaBlobImageCacheOptions`, and the cache is an `IResizedImageCache` used by the built-in NetVips pipeline (no ImageSharp.Web).
- Gen2 accounts now get atomic moves and real directories automatically; set `UseHierarchicalNamespace` when connecting with a container-scoped SAS.
