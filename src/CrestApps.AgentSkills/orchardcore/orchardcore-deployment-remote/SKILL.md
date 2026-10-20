---
name: orchardcore-deployment-remote
description: Skill for configuring remote deployment import and export in Orchard Core. Covers RemoteInstance, RemoteClient, RemoteInstanceService, RemoteClientService, the remote import endpoint, API key handling, DeploymentPermissions, upload validation of imported packages, and remote deployment administration. Use this skill when requests mention Orchard Core Remote Deployment, remote import, remote export, RemoteInstance, RemoteClient, deployment API client, remote deployment target, ImportRemoteInstance, ManageRemoteInstances, ExportRemoteInstances, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include work with OrchardCore.Deployment.Remote, OrchardCore.Deployment, IDeploymentTargetProvider, RemoteInstanceDeploymentTargetProvider, RemoteInstanceService, RemoteClientService, ExportRemoteInstanceController, and ImportRemoteInstanceController.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Remote Deployment

`OrchardCore.Deployment.Remote` extends the Deployment module with remote
import and export. It registers `RemoteInstanceService`, `RemoteClientService`,
an `IDeploymentTargetProvider`, HTTP client support, admin navigation, and
permissions. Remote deployments should be treated as privileged administrative
operations because they can move site configuration and content.

## Guidelines

- Enable `OrchardCore.Deployment.Remote`; its manifest depends on `OrchardCore.Deployment`.
- Configure a remote client on the destination and a remote instance on the source.
- Use HTTPS remote URLs and unique high-entropy API keys.
- Give remote-deployment permissions only to trusted administrators or service accounts.
- A `RemoteInstance` stores a remote URL, client name, and API key; protect access to all three.
- `RemoteClientService` protects client API keys with a time-limited data protector before persistence.
- `RemoteInstanceService` stores remote instance data through its document manager.
- Never copy an API key into public recipes, theme files, or application logs.
- Manage clients and instances under **Tools** → **Deployments** → **Remote Clients** and **Remote Instances**.
- Test with a non-production tenant before exchanging a production deployment plan.

## Enable Remote Deployment

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Deployment",
        "OrchardCore.Deployment.Remote"
      ],
      "disable": []
    }
  ]
}
```

## Permissions

All remote permissions are static members of `OrchardCore.Deployment.DeploymentPermissions` and are granted to `Administrator` by default:

| Member | Name | Purpose |
|---|---|---|
| `ManageRemoteInstances` | Manage remote instances | Create, edit, and delete remote instances (and see their API keys). |
| `ManageRemoteClients` | Manage remote clients | Create, edit, and delete remote clients on the destination. |
| `ExportRemoteInstances` | Export to remote instances | Deploy to a remote instance. |
| `Export` | Export Data | Required to execute any deployment plan, including to a remote instance. |

Grant `Export` and `ExportRemoteInstances` without `ManageRemoteInstances` to operators who may deploy but must not create or reveal credentials.

## Remote Clients and Instances

A destination remote client is represented by `RemoteClient`:

| Property | Meaning |
|---|---|
| `Id` | Generated identifier. |
| `ClientName` | Name selected by a remote instance. |
| `ProtectedApiKey` | Data-protected API-key bytes. |

A source remote instance is represented by `RemoteInstance`:

| Property | Meaning |
|---|---|
| `Name` | Admin-facing instance name. |
| `Url` | Remote import endpoint, e.g. `https://example.com/OrchardCore.Deployment.Remote/ImportRemoteInstance/Import` (add the tenant URL prefix for a non-default tenant). |
| `ClientName` | Destination client identity. |
| `ApiKey` | Credential used against the remote client. |

Create matching credentials at both ends through the remote deployment admin
screens. The client name and API key must match. Do not assume a remote
instance record itself grants access; the remote site must have a client that
accepts its credentials.

## Remote Import and Export Flow

1. Enable Deployment and Remote Deployment on both intended sites.
2. Create a remote client on the target site and record its client name and key securely.
3. Create a remote instance on the source with the target URL and matching credentials.
4. Build and validate a deployment plan on the source.
5. Select the remote instance as the deployment target and export.
6. Inspect the target site and deployment results before relying on automation.

The source builds a `.zip` package and sends it as a multipart `POST`. The
destination validates the client name and API key, runs the uploaded package
through the file upload security pipeline (`FileCreationService`, so an
enabled antivirus scanner such as `OrchardCore.Antivirus.ClamAV` can reject
it), writes it under the tenant-scoped temporary directory
(`ITempDirectoryProvider`, `OrchardCore:TempDirectory:Path`), and executes
`Recipe.json`. A rejected upload returns `400 Bad Request` with the reason.

The module exposes import and export controllers plus the remote instance
deployment target provider. Prefer the user interface for normal operations so
permissions and validation remain in the intended request path.

## Use the Services in a Module

Use the services only for a focused administrative integration:

```csharp
using OrchardCore.Deployment.Remote.Services;

namespace MyModule;

public sealed class RemoteDeploymentLookup
{
    private readonly RemoteInstanceService _remoteInstanceService;

    public RemoteDeploymentLookup(RemoteInstanceService remoteInstanceService)
    {
        _remoteInstanceService = remoteInstanceService;
    }

    public Task<OrchardCore.Deployment.Remote.Models.RemoteInstance> GetAsync(string id)
    {
        return _remoteInstanceService.GetRemoteInstanceAsync(id);
    }
}
```

Use `GetRemoteInstanceAsync` for cached read-only lookup and
`LoadRemoteInstanceAsync` when an update is intended. Create and update
operations persist changes through the service instead of modifying a document
list directly.

## Security and Troubleshooting

On failure, verify remote URL reachability (endpoint path and tenant prefix),
TLS certificates, client name, credential pairing, enabled features,
permissions, reverse-proxy limits on multipart `POST` size, and upload
validation errors in the destination logs. Rotate an API key by
updating both the remote client and every associated remote instance. Treat a
deployment payload as sensitive until its content and target are confirmed.

## Service Responsibilities

`RemoteInstanceService` has distinct read and update APIs. Use
`GetRemoteInstanceListAsync` and `GetRemoteInstanceAsync` for cached immutable
reads. Use `LoadRemoteInstanceListAsync` and `LoadRemoteInstanceAsync` before
editing data, then use the service create, update, or delete methods to write
through the document manager.

`RemoteClientService` loads its persisted client list through YesSql and
protects API keys using a data protector scoped to `OrchardCore.Deployment`.
Call its create or update methods rather than serializing an API key into a
custom content item. This preserves the module's credential-protection design.

## Plan Compatibility

Remote deployment transports an Orchard deployment plan; it does not ensure
that a target has every required module, feature, content definition, or
external dependency. Before export, compare enabled features and package
versions. Import foundational definitions first where a plan depends on them,
and validate the target in a staging tenant.

## Operational Guardrails

- Use a dedicated remote client for each source environment.
- Restrict outbound network access to known destination hosts when possible.
- Record the plan name and target in deployment audit logs without recording API keys.
- Review remote instance URLs after domain migrations or reverse-proxy changes.
- Rotate credentials immediately when a source site or administrator is retired.
- Avoid bidirectional automatic deployment between two production sites.

Remote deployment is appropriate for deliberate administration. It is not a
general-purpose replication protocol or a substitute for database backups.

## Upgrading from 3.x

- `OrchardCore.Deployment.Remote.Permissions.ManageRemoteInstances`, `ManageRemoteClients`, and `Export` were removed. Use `DeploymentPermissions.ManageRemoteInstances`, `DeploymentPermissions.ManageRemoteClients`, and `DeploymentPermissions.ExportRemoteInstances`. Stored role permission names are unchanged.
- Remote export and import archives use `ITempDirectoryProvider` instead of `Path.GetTempPath()`; set `OrchardCore:TempDirectory:Path` when the system temp volume is small.
- Imported packages now pass through `FileCreationService`, so file upload security handlers can reject them.
