---
name: orchardcore-ai-a2a-host
description: Skill for the CrestApps Orchard Core A2A Host feature (CrestApps.OrchardCore.AI.A2A.Host), which exposes AI profiles of type Agent through the Agent-to-Agent protocol. Covers the public /.well-known/agent-card.json discovery endpoint, A2A 1.0 agent cards that publish their endpoint in supportedInterfaces with the JSON-RPC binding instead of the deprecated url field, multi-agent mode with /a2a?agent={agentName} versus ExposeAgentsAsSkill skill mode with agentName metadata routing, A2AHostOptions configured under OrchardCore:CrestApps:AI:A2AHost (AuthenticationType OpenId, ApiKey or None, ApiKey, RequireAccessPermission), security schemes advertised on the card, the AccessA2AHost permission, AI usage metered under the A2A category, and creating Agent profiles with the AIProfile recipe step. Use this skill when requests mention Orchard Core A2A host, exposing agents to other AI apps, agent cards, supportedInterfaces, ExposeAgentsAsSkill, or troubleshooting A2A clients that cannot reach an Orchard Core agent.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core A2A Host

The A2A Host feature makes every AI profile of type **Agent** discoverable and callable by external A2A clients, including another Orchard Core site running the A2A Client feature (`orchardcore-ai-a2a-client`).

## Guidelines

- Feature ID `CrestApps.OrchardCore.AI.A2A.Host`, in the `CrestApps.OrchardCore.AI.A2A` package (install it in the web/startup project). It depends only on `CrestApps.OrchardCore.AI`.
- Only profiles whose `Type` is `Agent` are exposed. Their `Name` identifies the agent and their `Description` becomes the card and skill description.
- Cards follow A2A 1.0: each card publishes its endpoint in `supportedInterfaces` (protocol binding JSON-RPC). The deprecated top-level `url` is no longer emitted.
- The discovery endpoint is always anonymous so clients can read the authentication requirements. The `/a2a` endpoint is protected by the `A2AHostPolicy` policy.
- Options come from shell configuration only (no admin settings page). Default authentication is `OpenId`.
- Use `None` only for local development.

## Enable the feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.A2A.Host"
      ],
      "disable": []
    }
  ]
}
```

## Create agent profiles

Create an AI profile with **Profile type** set to **Agent** in the admin, or import one. Bind deployments by technical name; see `orchardcore-ai` for the full profile contract.

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "research-agent",
          "DisplayText": "Research Agent",
          "Description": "Researches topics and returns sourced summaries.",
          "Type": "Agent",
          "ChatDeploymentName": "research-chat",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a research assistant.",
              "Temperature": 0.2,
              "MaxTokens": 4096
            },
            "AgentMetadata": {
              "Availability": "OnDemand"
            }
          }
        }
      ]
    }
  ]
}
```

## Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /.well-known/agent-card.json` | Discovery. A JSON array of cards in multi-agent mode, one combined card in skill mode. |
| `/a2a` | A2A JSON-RPC endpoint. |
| `/a2a?agent={agentName}` | Targets one agent in multi-agent mode. |

Messages are routed by the `agent` query parameter first, then by an `agentName` entry in the message metadata, and otherwise to the first agent profile. The host supports streaming; task subscription and push notifications are not supported.

### Exposure modes

| Mode | Setting | Card shape |
|---|---|---|
| Multi-agent (default) | `ExposeAgentsAsSkill: false` | One card per agent; each `supportedInterfaces` entry points to `/a2a?agent={agentName}` |
| Skill | `ExposeAgentsAsSkill: true` | One card whose skills are the agents; clients route with `agentName` metadata |

A multi-agent card looks like this (trimmed):

```json
[
  {
    "name": "Research Agent",
    "description": "Researches topics and returns sourced summaries.",
    "supportedInterfaces": [
      { "url": "https://example.com/a2a?agent=research-agent", "protocolBinding": "JSONRPC" }
    ],
    "capabilities": { "streaming": true }
  }
]
```

## Configuration

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "A2AHost": {
          "AuthenticationType": "ApiKey",
          "ApiKey": "{{A2AHostApiKey}}",
          "RequireAccessPermission": true,
          "ExposeAgentsAsSkill": false
        }
      }
    }
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `AuthenticationType` | `OpenId` | `OpenId`, `ApiKey`, or `None` |
| `ApiKey` | | Key required in `ApiKey` mode |
| `RequireAccessPermission` | `true` | Authenticated callers also need `AccessA2AHost` |
| `ExposeAgentsAsSkill` | `false` | Combine all agents into one card |

The older `OrchardCore:CrestApps:A2AHost` section is still read first for backward compatibility; prefer `OrchardCore:CrestApps:AI:A2AHost`. Configuration changes need an app restart.

### Authentication on the card

| Mode | Advertised security scheme | Client sends |
|---|---|---|
| `OpenId` | `openId`, with `{host}/.well-known/openid-configuration` | `Authorization: Bearer <token>` from the Orchard Core OpenID server (`Api` scheme) |
| `ApiKey` | `apiKey` in the `Authorization` header | `Authorization: Bearer <key>` or `Authorization: ApiKey <key>` |
| `None` | none | nothing |

`AccessA2AHost` is security critical and is not granted to any role by default; grant it to the role or OpenID application that calls the host.

## Usage analytics

Completions run for A2A requests are labelled with the **A2A** usage category, so they appear under that category in **AI Usage Analytics** (`orchardcore-ai-usage-analytics`).

## Upgrading from 2.x

- Cards no longer contain the top-level `url`. Clients must read the endpoint from `supportedInterfaces`. The CrestApps A2A client already does.
- Multi-agent mode still points each interface at `/a2a?agent={agentName}`.
- Move configuration from `OrchardCore:CrestApps:A2AHost` to `OrchardCore:CrestApps:AI:A2AHost`.

## Troubleshooting

| Symptom | Check |
|---|---|
| The card array is empty | No AI profile has type **Agent**. |
| A client reports a missing endpoint | It still reads `url`; update it to A2A 1.0 `supportedInterfaces`. |
| 401 or 403 on `/a2a` | Credentials do not match `AuthenticationType`, or the caller lacks `AccessA2AHost`. |
| Card URLs miss the tenant prefix | Card URLs are built from the request scheme and host. Verify the advertised interface URL on prefixed tenants and behind reverse proxies. |
| The wrong agent answers | Send `?agent={name}` (multi-agent) or `agentName` metadata (skill mode); otherwise the first agent answers. |
