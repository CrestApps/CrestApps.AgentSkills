---
name: orchardcore-azure-data-protection
description: Skill for configuring Azure Data Protection in Orchard Core. Covers distributed data protection key storage using Azure Blob Storage, connection string configuration, per-tenant container templating, and persistence considerations for multi-instance deployments.
---

# Orchard Core Azure Data Protection - Prompt Templates

## Module Overview

The `OrchardCore.DataProtection.Azure` module enables storing ASP.NET Core Data Protection key rings in Azure Blob Storage. This is essential for load-balanced and multi-instance deployments where all instances must share the same key ring.

Data Protection is a core ASP.NET Core security mechanism used by Orchard Core to safeguard:

- Authentication cookies
- Anti-forgery tokens
- Decryptable persisted secrets (e.g., SMTP credentials, excluding user passwords)
- Temporary data (TempData)

Keys are automatically isolated per tenant within the configured container.

## Configuration

### Guidelines

- Enable the `OrchardCore.DataProtection.Azure` feature.
- Provide a valid Azure Storage account connection string.
- The container name must be a valid DNS name and conform to Azure naming rules (lowercase only).
- Set `CreateContainer` to `true` (default) to auto-create the container on startup.
- This feature is critical for multi-instance deployments (load-balanced, scaled-out) where all instances must share the same data protection keys.
- Without shared key storage, authentication cookies and anti-forgery tokens will fail across instances.

### Configuration Properties

| Property | Description | Default |
|---|---|---|
| ConnectionString | Azure Storage account connection string (required). | `""` |
| ContainerName | Azure Blob container name (lowercase, valid DNS name). | `"dataprotection"` |
| BlobName | Specific blob name for storing keys (optional, defaults to tenant-specific path). | `""` |
| CreateContainer | Whether to auto-create the container if it doesn't exist. | `true` |

### Basic Configuration

Add to `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_DataProtection_Azure": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
      "ContainerName": "dataprotection",
      "BlobName": "",
      "CreateContainer": true
    }
  }
}
```

### Default Key Storage Path

By default, keys are stored in a folder-per-tenant structure within a single container:

```
dataprotection/Sites/tenant_name/DataProtectionKeys.xml
```

## Multi-Tenant Configuration with Liquid Templating

### Guidelines

- Use Liquid templating in `ContainerName` and `BlobName` properties for per-tenant isolation.
- The `ShellSettings` object is available in the Liquid template context.
- `{{ ShellSettings.Name }}` is automatically lowercased; ensure `ContainerName` conforms to Azure naming rules.
- If `BlobName` is not supplied, it defaults to `Sites/tenant_name/DataProtectionKeys.xml`.
- Only default Liquid filters and tags are available; extra filters like `slugify` are not supported.

### Container per Tenant

```json
{
  "OrchardCore": {
    "OrchardCore_DataProtection_Azure": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
      "ContainerName": "{{ ShellSettings.Name }}-dataprotection",
      "BlobName": "{{ ShellSettings.Name }}DataProtectionKeys.xml",
      "CreateContainer": true
    }
  }
}
```

### Single Container with Tenant Isolation (Default Behavior)

The default configuration stores all tenants' keys in one container, isolated by folder:

```json
{
  "OrchardCore": {
    "OrchardCore_DataProtection_Azure": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
      "ContainerName": "dataprotection",
      "CreateContainer": true
    }
  }
}
```

This produces the path: `dataprotection/Sites/{tenant_name}/DataProtectionKeys.xml`

## Multi-Instance Deployment Considerations

### Guidelines

- In a single-instance deployment, the default file-based key storage works fine.
- In load-balanced or multi-instance deployments, all instances must share the same data protection keys.
- Without `OrchardCore.DataProtection.Azure`, each instance generates its own key ring, causing authentication failures when requests hit different instances.
- Enable this feature before deploying to production with multiple instances.
- The `CreateContainer` option checks for container existence on each startup; set to `false` once the container is confirmed to exist, to skip this check.

### Symptoms of Missing Shared Key Storage

- Users are randomly logged out when requests are routed to different instances.
- Anti-forgery token validation errors on form submissions.
- Encrypted data (e.g., SMTP passwords) cannot be decrypted by other instances.
- Session data lost across instances.

### Recommended Production Configuration

```json
{
  "OrchardCore": {
    "OrchardCore_DataProtection_Azure": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=prodaccount;AccountKey=prodkey;EndpointSuffix=core.windows.net",
      "ContainerName": "dataprotection",
      "CreateContainer": false
    }
  }
}
```

Set `CreateContainer` to `false` in production when the container is pre-provisioned, to avoid unnecessary startup checks.
