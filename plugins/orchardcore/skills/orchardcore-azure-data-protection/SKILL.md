---
name: orchardcore-azure-data-protection
description: Skill for configuring Azure Data Protection in Orchard Core. Covers storing the ASP.NET Core Data Protection key ring in Azure Blob Storage with the OrchardCore.DataProtection.Azure feature, the OrchardCore DataProtection Azure configuration section, environment variable and Key Vault secret names, ConnectionString, ContainerName, BlobName and CreateContainer, per-tenant container and blob templating with ShellSettings, and why multi-instance deployments must share keys for cookies, antiforgery tokens, encrypted settings, and OpenID access tokens used by the Media Gallery. Use this skill when requests mention Orchard Core Azure Data Protection, shared data protection keys, DataProtectionKeys.xml, load-balanced Orchard Core, random logouts or antiforgery failures across instances, upgrading from the OrchardCore_DataProtection_Azure section, or closely related Orchard Core implementation, setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure Data Protection

## Module Overview

`OrchardCore.DataProtection.Azure` stores the ASP.NET Core Data Protection key
ring in Azure Blob Storage, isolated per tenant. All instances of a load-balanced
deployment must read the same key ring.

Orchard Core uses Data Protection for:

- Authentication cookies and antiforgery tokens.
- Decryptable persisted secrets (SMTP passwords, API keys; not user passwords).
- OpenID Connect server tokens in the Data Protection format (for example the Media Gallery bearer tokens).
- Temporary data (TempData).

## Configuration (`OrchardCore:DataProtection:Azure`)

### Guidelines

- Enable the `OrchardCore.DataProtection.Azure` feature and provide a connection string through `IShellConfiguration` (appsettings, environment variables, Key Vault).
- The container name must be a valid lowercase DNS name.
- Without a connection string the feature logs a critical error and adds no services; the default per-instance key storage stays in use.
- `CreateContainer` (default `true`) checks for the container on startup and creates it; set `false` when it is pre-provisioned.

| Property | Description | Default |
|---|---|---|
| `ConnectionString` | Storage account connection string (required). | |
| `ContainerName` | Blob container. Templatable. | `dataprotection` |
| `BlobName` | Key ring blob. Templatable. | `Sites/{tenant}/DataProtectionKeys.xml` |
| `CreateContainer` | Create the container if missing. | `true` |

```json
{
  "OrchardCore": {
    "DataProtection": {
      "Azure": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
        "ContainerName": "dataprotection",
        "BlobName": "",
        "CreateContainer": true
      }
    }
  }
}
```

- Environment variable: `OrchardCore__DataProtection__Azure__ConnectionString`.
- Azure Key Vault secret (see `orchardcore-azure-key-vault`): `OrchardCore--DataProtection--Azure--ConnectionString`.

### Default Key Storage Path

One container, one folder per tenant:

```
dataprotection/Sites/{tenant}/DataProtectionKeys.xml
```

## Multi-Tenant Templating

- `ContainerName` and `BlobName` accept Liquid with `ShellSettings` in scope.
- `{{ ShellSettings.Name }}` is lowercased; the container name must still follow Azure naming rules.
- Only default Liquid filters and tags are available (no `slugify`).

### Container per Tenant

```json
{
  "OrchardCore": {
    "DataProtection": {
      "Azure": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
        "ContainerName": "{{ ShellSettings.Name }}-dataprotection",
        "BlobName": "{{ ShellSettings.Name }}DataProtectionKeys.xml",
        "CreateContainer": true
      }
    }
  }
}
```

## Multi-Instance Deployments

- A single instance works with the default file-based key storage.
- With several instances, each one otherwise generates its own key ring, so a cookie, antiforgery token or access token issued by one instance is rejected by another.
- Enable this feature (or `OrchardCore.Redis.DataProtection`, see `orchardcore-redis`) before scaling out. Pick one of them per tenant.
- The Media Gallery in Cookie or Bearer mode depends on shared keys; per-instance keys surface as intermittent `401` errors behind a round-robin load balancer (see `orchardcore-media-gallery`).

### Symptoms of Missing Shared Key Storage

- Users are randomly logged out when requests reach a different instance.
- Antiforgery validation errors on form posts and Media API uploads.
- Encrypted settings (for example SMTP passwords) cannot be decrypted by other instances.
- OpenID access tokens rejected with `401` by other instances.

### Recommended Production Configuration

```json
{
  "OrchardCore": {
    "DataProtection": {
      "Azure": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=prodaccount;AccountKey=prodkey;EndpointSuffix=core.windows.net",
        "ContainerName": "dataprotection",
        "CreateContainer": false
      }
    }
  }
}
```

Keep the connection string out of source control: use an environment variable
or a Key Vault secret.

## Upgrading from 3.x

- Rename the `OrchardCore_DataProtection_Azure` section to `DataProtection:Azure`.
- Environment variable `OrchardCore__OrchardCore_DataProtection_Azure__ConnectionString` becomes `OrchardCore__DataProtection__Azure__ConnectionString`.
- Key Vault secret `OrchardCore--OrchardCore---DataProtection---Azure--ConnectionString` becomes `OrchardCore--DataProtection--Azure--ConnectionString`.
- The old names are deprecated but still read; the new name wins when both are set.
