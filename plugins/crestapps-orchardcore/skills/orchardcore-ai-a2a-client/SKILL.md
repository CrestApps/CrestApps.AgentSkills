---
name: orchardcore-ai-a2a-client
description: Skill for the CrestApps Orchard Core A2A Client feature (CrestApps.OrchardCore.AI.A2A), which connects AI profiles to remote Agent-to-Agent hosts. Covers the Agent to Agent Hosts admin screen, endpoint and agent-card discovery at /.well-known/agent-card.json with A2A 1.0 supportedInterfaces, A2AConnectionMetadata authentication types (Anonymous, ApiKey, Basic, OAuth2ClientCredentials, OAuth2PrivateKeyJwt, OAuth2Mtls, CustomHeaders), the A2AConnection recipe step, encrypted credentials, the ManageA2AConnections permission, assigning A2A connections to AI profiles, profile templates, chat interactions and the AI Completion using Direct Config workflow task, remote agents surfaced as tools through A2AAgentProxyTool, and the listAvailableAgents, findAgentForTask and findToolsForTask functions. Use this skill when requests mention Orchard Core A2A client, agent connections, calling a remote agent, multi-agent orchestration across sites, or troubleshooting remote agents that do not appear.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core A2A Client

The A2A Client feature lets Orchard Core AI experiences call agents hosted on other A2A servers, including other Orchard Core sites running the A2A Host feature (`orchardcore-ai-a2a-host`).

## Guidelines

- Feature ID `CrestApps.OrchardCore.AI.A2A`, from the `CrestApps.OrchardCore.AI.A2A` package (install it in the web/startup project). It depends on `CrestApps.OrchardCore.AI` and `CrestApps.OrchardCore.Resources`.
- Connections are managed under **Artificial Intelligence → Agent to Agent Hosts** (permission `ManageA2AConnections`, granted to Administrator by default).
- A connection points at the base URL of an A2A host. The agent card is resolved from `/.well-known/agent-card.json` and cached for 15 minutes.
- Agent endpoints are read from the card's A2A 1.0 `supportedInterfaces`. Older cards that only publish a top-level `url` are still handled.
- Each remote agent skill becomes a tool the model can call. Keep remote descriptions accurate, because they drive tool selection.
- Credentials are encrypted at rest with ASP.NET Core Data Protection.

## Enable the feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.A2A"
      ],
      "disable": []
    }
  ]
}
```

## Add a connection

1. Open **Artificial Intelligence → Agent to Agent Hosts** and click **Add Connection**.
2. Enter a **Title**, the host **Endpoint** (for example `https://agents.example.com`), and an **Authentication** method.
3. Save. The list (`AdminList` name `A2AConnections`, see `orchardcore-admin-lists`) shows each connection with its endpoint.

### Authentication types (`A2AConnectionMetadata.AuthenticationType`)

| Value | Fields |
|---|---|
| `Anonymous` | None (default) |
| `ApiKey` | `ApiKey`, `ApiKeyHeaderName`, optional `ApiKeyPrefix` such as `Bearer` |
| `Basic` | `BasicUsername`, `BasicPassword` |
| `OAuth2ClientCredentials` | `OAuth2TokenEndpoint`, `OAuth2ClientId`, `OAuth2ClientSecret`, `OAuth2Scopes` |
| `OAuth2PrivateKeyJwt` | `OAuth2TokenEndpoint`, `OAuth2ClientId`, `OAuth2PrivateKey` (PEM), optional `OAuth2KeyId`, `OAuth2Scopes` |
| `OAuth2Mtls` | `OAuth2TokenEndpoint`, `OAuth2ClientId`, `OAuth2ClientCertificate` (Base64 PFX), optional `OAuth2ClientCertificatePassword`, `OAuth2Scopes` |
| `CustomHeaders` | `AdditionalHeaders` as a JSON object |

To call an Orchard Core A2A host in `ApiKey` mode, use `ApiKeyHeaderName` `Authorization` and prefix `ApiKey` or `Bearer`.

## Recipe step

The `A2AConnection` step creates connections, or updates the one whose `ItemId` matches. `DisplayText` and `Endpoint` are required, and the endpoint must be an absolute HTTP or HTTPS URL.

```json
{
  "steps": [
    {
      "name": "A2AConnection",
      "Connections": [
        {
          "DisplayText": "Production agent hub",
          "Endpoint": "https://agents.example.com",
          "Properties": {
            "A2AConnectionMetadata": {
              "AuthenticationType": "ApiKey",
              "ApiKeyHeaderName": "Authorization",
              "ApiKeyPrefix": "ApiKey",
              "ApiKey": "{{AgentHubApiKey}}"
            }
          }
        }
      ]
    }
  ]
}
```

Secrets supplied in the recipe are encrypted on import. The module registers no deployment step for A2A connections, so recreate them per environment with a recipe.

## Assign connections

| Surface | Where |
|---|---|
| AI Profile | **Capabilities** tab, **A2A Connections** |
| AI Profile Template (Profile source) | **Capabilities** tab, **A2A Connections** |
| Chat Interaction | **Capabilities** tab, **A2A Connections** |
| AI Completion using Direct Config workflow task | **Capabilities** tab (needs `CrestApps.OrchardCore.AI.Chat.Interactions` and `OrchardCore.Workflows`) |

The section renders only when at least one connection exists.

## How remote agents run

1. The client fetches and caches each selected host's agent card.
2. Each agent skill on the card is registered as an AI tool.
3. When the model picks one, `A2AAgentProxyTool` sends the message to the agent's interface URL with the connection's credentials.
4. The remote reply is returned to the model as tool output.

The client also contributes system functions the orchestrator can use:

| Function | Purpose |
|---|---|
| `listAvailableAgents` | Lists local and remote agents |
| `findAgentForTask` | Finds the best-matching agent for a task |
| `findToolsForTask` | Finds the best-matching AI tools for a task |

## Upgrading from 2.x

- Remote endpoints are read from `supportedInterfaces` (A2A 1.0). Hosts that still publish only `url` keep working.
- The connections list now uses Orchard Core's `AdminList` shape; theme overrides of the old row template apply only to the List layout.

## Troubleshooting

| Symptom | Check |
|---|---|
| No remote tools appear | The connection is not selected on the profile, the card fetch failed, or the card was cached before the host changed (wait 15 minutes or re-save the connection). |
| 401 from the remote host | Authentication type, header name, or prefix does not match what the host expects. |
| Calls reach the wrong URL | Inspect the host's agent card `supportedInterfaces`; the client calls the URL the card advertises. |
| Validation error on import | `Endpoint` is missing or not an absolute HTTP or HTTPS URL. |
