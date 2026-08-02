---
name: orchardcore-ai-documents-azure-storage
description: Skill for storing CrestApps Orchard Core AI Documents in Azure Blob Storage. Covers AIDocumentBlobStorageOptions, BlobFileStore replacement, tenant container lifecycle, cleanup choices, and Azure configuration. Use this skill when requests mention Orchard Core AI Documents Azure Blob Storage, AIDocumentBlobStorageOptions, AIDocumentBlobContainerTenantEvents, document file storage, or Azure document containers. Strong matches include work with CrestApps.OrchardCore.AI.Documents.Azure, IDocumentFileStore, DefaultDocumentFileStore, BlobFileStore, MediaBlobStorageOptionsBase, and IModularTenantEvents.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Documents Azure Blob Storage

## Configure Azure Blob document storage

You are an Orchard Core expert. Configure CrestApps AI Documents to place uploaded
document files in Azure Blob Storage while preserving the normal document extraction,
chunking, embedding, indexing, and RAG workflow.

### Guidelines

- Enable the exact feature ID `CrestApps.OrchardCore.AI.Documents.Azure`.
- This feature depends on AI Documents through `ChatInteractionsConstants.Feature.ChatDocuments`; it enables the base document capability by dependency rather than replacing its processing pipeline.
- The feature changes only the `IDocumentFileStore` implementation. It does not create an index, generate embeddings, or select a vector-search backend.
- If either `ConnectionString` or `ContainerName` is empty, the feature logs an error and leaves the existing `IDocumentFileStore` registration unchanged rather than registering the Azure-backed replacement.
- Install `CrestApps.OrchardCore.AI.Documents.Azure` in the web or startup project.
- Keep connection strings in user secrets, environment variables, managed identity configuration, or a production secret store; never place production keys in recipes or source control.
- Use a lowercase Azure-valid container name. Configuration normalizes the container name to lowercase.
- Treat the container lifecycle options carefully for multi-tenant installations, especially if tenants share one container.

### What the feature registers

| Registration | Purpose |
|---|---|
| `AIDocumentBlobStorageOptionsConfiguration` | Binds the tenant shell configuration to `AIDocumentBlobStorageOptions`. |
| `BlobFileStore` | Orchard Core Azure Blob file-system implementation. |
| `DefaultDocumentFileStore` | Wraps the blob store and replaces the default `IDocumentFileStore`. |
| `AIDocumentBlobContainerTenantEvents` | Creates or removes tenant storage during tenant lifecycle events. |

`AIDocumentBlobStorageOptions` derives from `MediaBlobStorageOptionsBase`. Its storage
settings therefore include the connection string, container name, base path, create,
and removal behavior.

### Enable the feature

Enable an AI Documents context or indexing backend as appropriate, then enable Azure
document storage. The base AI Documents capability is enabled by dependency.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.Azure",
        "CrestApps.OrchardCore.AI.Documents.AzureAI"
      ],
      "disable": []
    }
  ]
}
```

The storage feature can also be used with `CrestApps.OrchardCore.AI.Documents.Elasticsearch`,
Profiles, or Chat Sessions. Do not enable a search provider merely to use Blob Storage
unless the application also needs indexed retrieval.

### Configure tenant settings

Configure the tenant under `OrchardCore:CrestApps:AI:AzureDocuments`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "AzureDocuments": {
          "ConnectionString": "Use a secret or environment-variable value here",
          "ContainerName": "ai-documents",
          "BasePath": "tenants/default/documents",
          "CreateContainer": true,
          "RemoveContainer": false,
          "RemoveFilesFromBasePath": true
        }
      }
    }
  }
}
```

| Option | Behavior |
|---|---|
| `ConnectionString` | Azure Storage account connection string required to activate the replacement store. |
| `ContainerName` | Required blob container. Use a lowercase name that meets Azure naming rules. |
| `BasePath` | Optional prefix for AI document files. It supports Orchard Core shell-token formatting. |
| `CreateContainer` | Creates the container when a configured tenant activates. |
| `RemoveContainer` | Deletes the entire container during tenant removal. |
| `RemoveFilesFromBasePath` | Deletes only blobs beneath `BasePath` during tenant removal. |

`RemoveContainer` takes precedence over `RemoveFilesFromBasePath`. Never enable
`RemoveContainer` for a container shared by tenants or other applications.

### Tenant lifecycle behavior

`AIDocumentBlobContainerTenantEvents` runs only when valid storage options are present.
On activation, it skips uninitialized tenants and creates the configured container only
when `CreateContainer` is true. The container is created without public access.

On tenant removal:

1. If `RemoveContainer` is true, it deletes the whole configured container.
2. Otherwise, if `RemoveFilesFromBasePath` is true, it enumerates blobs with the base
   path prefix and deletes each blob.
3. Azure request failures are logged and are surfaced through the tenant removal
   context so the administrator can act on the failed cleanup.

Choose a unique container per tenant when whole-container cleanup is desired. Choose
a tenant-specific `BasePath` and `RemoveFilesFromBasePath` when tenants share a
container.

### Storage flow

1. A user uploads a document through a supported AI Documents context.
2. `IDocumentFileStore` writes the file through `DefaultDocumentFileStore`.
3. With valid Azure settings, that wrapper uses `BlobFileStore`; otherwise this module
   does not replace the application's existing document-file-store registration.
4. The usual document processor extracts text, chunks it, and optionally indexes the
   resulting chunks through the enabled search backend.

Blob Storage does not make a file publicly accessible and does not alter authorization
for upload, download, or document removal.

### Example deployment layout

For a shared account and container, isolate tenants by path:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "AzureDocuments": {
          "ConnectionString": "Set through a production secret provider",
          "ContainerName": "ai-documents",
          "BasePath": "tenants/{ ShellName }/documents",
          "CreateContainer": true,
          "RemoveContainer": false,
          "RemoveFilesFromBasePath": true
        }
      }
    }
  }
}
```

Verify the shell-token syntax and the resolved path for the Orchard Core version in
use before relying on it for retention isolation.

### Troubleshooting

| Symptom | Check |
|---|---|
| Azure storage is not active | Confirm both required settings are non-empty and restart or reactivate the tenant after changing configuration. The module otherwise preserves the pre-existing document-file-store registration. |
| Feature logs configuration errors | Verify the exact `OrchardCore:CrestApps:AI:AzureDocuments` path and use a valid connection string and container. |
| Container is not created | Set `CreateContainer` to true and confirm the storage identity can create containers. |
| Tenant removal deletes too much | Disable `RemoveContainer` for shared containers and use a tenant-specific base path with `RemoveFilesFromBasePath`. |
| Tenant removal cannot clean up | Confirm delete permissions and inspect the tenant-removal error and Azure request logs. |
| RAG retrieval is unavailable | Blob storage is not an index provider. Enable and configure Azure AI Search or Elasticsearch plus an embedding deployment. |

### Security checklist

- Grant the storage identity only the blob permissions needed for the selected lifecycle options.
- Prefer private endpoints and encrypted transport for production storage accounts.
- Keep blob containers private; this module creates containers with `PublicAccessType.None`.
- Establish retention and tenant-deletion policy before setting cleanup flags.
