---
name: orchardcore-azure-media
description: Skill for configuring Azure Blob Storage for media in Orchard Core. Covers Azure Blob connection setup, container configuration, Liquid templating for per-tenant containers, Media Cache integration, ImageSharp configuration, and CDN considerations. Use this skill when requests mention Orchard Core Azure Media Storage, Azure Media Storage Configuration, Configuration Properties, Basic Configuration, Liquid Templating for Multi-Tenant Configuration, Container per Tenant, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Media.Azure, OrchardCore.Media.Azure.Storage, ShellSettings, App_Data, AssetUrl, CreateContainer, ContainerName, BasePath, PhysicalFileSystemCache. It also helps with Liquid Templating for Multi-Tenant Configuration, Container per Tenant, Single Container with Base Folder per Tenant, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Azure Media Storage - Prompt Templates

## Module Overview

The `OrchardCore.Media.Azure` module enables storing media assets in Microsoft Azure Blob Storage instead of the default `App_Data` file-based store. It provides two features:

- **OrchardCore.Media.Azure.Storage** — Replaces the default media store with Azure Blob Storage.
- **OrchardCore.Media.Azure.ImageSharpImageCache** — Stores ImageSharp resized image cache in Azure Blob Storage instead of the local file system.

Media is still served by the Orchard Core web site. The Media Cache module fetches assets on the fly from Azure Blob Storage, enabling image resizing through ImageSharp.Web integration. The `AssetUrl` helpers generate URLs pointing to the Orchard Core web site.

## Azure Media Storage Configuration

### Guidelines

- Enable the `OrchardCore.Media.Azure.Storage` feature.
- Provide a valid Azure Storage account connection string.
- The container name must be a valid DNS name and conform to Azure container naming rules (lowercase only).
- Set `CreateContainer` to `true` to auto-create the container on startup; set to `false` if your container already exists.
- If the connection string is missing or invalid, the feature will not activate and an error will be logged.
- Only one storage provider can be active at a time (File Storage, Azure Blob Storage, or Amazon S3 Storage).

### Configuration Properties

| Property | Description | Default |
|---|---|---|
| ConnectionString | Azure Storage account connection string (required). | `""` |
| ContainerName | Azure Blob container name (lowercase, valid DNS name). | `""` |
| BasePath | Subdirectory path inside the container for media storage. | `""` |
| CreateContainer | Whether to create the container on startup if it doesn't exist. | `true` |
| RemoveContainer | Whether the container is deleted when the tenant is removed. | `false` |

### Basic Configuration

```json
{
  "OrchardCore": {
    "OrchardCore_Media_Azure": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
      "ContainerName": "media",
      "BasePath": "Media",
      "CreateContainer": true,
      "RemoveContainer": false
    }
  }
}
```

## Liquid Templating for Multi-Tenant Configuration

### Guidelines

- Use Liquid templating in `ContainerName` and `BasePath` properties for multi-tenant setups.
- The `ShellSettings` object is available in the Liquid template context.
- `{{ ShellSettings.Name }}` is automatically lowercased, but ensure the full `ContainerName` conforms to Azure naming rules.
- Only default Liquid filters and tags are available; extra filters like `slugify` are not supported.

### Container per Tenant

```json
{
  "OrchardCore": {
    "OrchardCore_Media_Azure": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
      "ContainerName": "{{ ShellSettings.Name }}-media",
      "BasePath": "Media",
      "CreateContainer": true
    }
  }
}
```

### Single Container with Base Folder per Tenant

```json
{
  "OrchardCore": {
    "OrchardCore_Media_Azure": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
      "ContainerName": "sharedmedia",
      "BasePath": "{{ ShellSettings.Name }}/Media",
      "CreateContainer": true
    }
  }
}
```

## Media Cache

### Guidelines

- The Media Cache feature is automatically enabled when Azure Media Storage is enabled.
- It caches files fetched from Azure Blob Storage locally to support image resizing via ImageSharp.
- The admin dashboard provides a **Purge** action to clear cached files.
- Consider purging only after a CDN has had sufficient time to cache most assets.
- CDN providers have multiple Points of Presence (PoPs), each maintaining its own cache. A purged local cache item will be re-fetched from Azure Blob Storage when a CDN PoP requests it.
- CDN providers periodically clear their own caches, so the Media Cache must always be able to re-fetch source files from Azure Blob Storage.

## Azure Media ImageSharp Image Cache

### Guidelines

- Enable the `OrchardCore.Media.Azure.ImageSharpImageCache` feature to store resized images in Azure Blob Storage.
- This replaces the default `PhysicalFileSystemCache` that stores resized images in `App_Data`.
- Useful for ephemeral file systems (e.g., container hosting, clean deployments).
- Reduces pressure on local disk IO, beneficial for environments like Azure App Services with throttled local storage.
- Cache files are only removed per tenant when using a separate container per tenant.

### ImageSharp Cache Configuration

| Property | Description | Default |
|---|---|---|
| ConnectionString | Azure Storage account connection string (required). | `""` |
| ContainerName | Container for storing resized image cache. | `""` |
| BasePath | Subdirectory path inside the container. | `""` |
| CreateContainer | Auto-create the container on startup. | `true` |
| RemoveContainer | Delete the container when the tenant is removed. | `false` |
| RemoveFilesFromBasePath | Delete files under the base path when the tenant is removed (only when `RemoveContainer` is `false`). | `false` |

### ImageSharp Cache Configuration Example

```json
{
  "OrchardCore": {
    "OrchardCore_Media_Azure_ImageSharp_Cache": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
      "ContainerName": "imagecache",
      "BasePath": "cache",
      "CreateContainer": true,
      "RemoveContainer": false,
      "RemoveFilesFromBasePath": true
    }
  }
}
```

## CDN Considerations

- When fronting media with a CDN, allow sufficient time for CDN PoPs to cache assets before purging the local Media Cache.
- Each CDN PoP maintains its own cache independently; one PoP having an asset does not mean all do.
- The Media Cache will automatically re-fetch assets from Azure Blob Storage when requested by a CDN PoP that doesn't have the cached version.
- CDN providers clear caches on their own schedules, so the source in Azure Blob Storage must always remain accessible.
