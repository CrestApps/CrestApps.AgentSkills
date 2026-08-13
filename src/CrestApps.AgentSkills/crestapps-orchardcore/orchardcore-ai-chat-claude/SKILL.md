---
name: orchardcore-ai-chat-claude
description: Skill for configuring the Claude chat orchestrator in Orchard Core using the CrestApps AI Chat Claude module. Covers tenant Claude options, encrypted Anthropic credentials, model discovery, Claude model and effort selection on AI profiles, profile templates, and chat interactions, plus permission management. Use this skill when requests mention Orchard Core Claude Orchestrator, ClaudeOptionsConfiguration, Claude Settings, Anthropic API Key, Claude Profiles, Claude Templates, Claude Chat Interactions, or closely related CrestApps implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.AI.Chat.Claude, ClaudeOptionsConfiguration, ClaudeSettingsDisplayDriver, AIProfileClaudeDisplayDriver, AIProfileTemplateClaudeDisplayDriver, ChatInteractionClaudeDisplayDriver, ClaudePermissionProvider.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Claude - Prompt Templates

## Configure the Claude Chat Orchestrator

You are an Orchard Core expert. Generate configuration, recipes, and extension code for Claude as an Orchard-managed **chat orchestrator** using CrestApps modules. Claude is not an AI provider connection or deployment provider in this integration.

### Guidelines
- Enable `CrestApps.OrchardCore.AI.Chat.Claude` to make the Claude orchestrator available to AI Profiles, profile-source AI Profile Templates, and Chat Interactions.
- The Claude feature depends on `CrestApps.OrchardCore.AI`; enable the feature rather than treating it as an OpenAI-compatible connection.
- `ClaudeOptionsConfiguration` binds shell configuration from `CrestApps:AI:Claude`, then overlays tenant `ClaudeSettings`.
- Tenant `BaseUrl` and `DefaultModel` override corresponding shell values only when populated.
- Use **Settings → Artificial Intelligence → Claude** to set tenant authentication, API key, base URL, and default model.
- API-key authentication stores `ProtectedApiKey` encrypted with ASP.NET Core Data Protection. Never put a production Anthropic key in a recipe, source file, or committed configuration.
- Claude is considered configured only when its authentication type is `ApiKey` and an encrypted API key exists.
- `ClaudeSettingsDisplayDriver` exposes the tenant settings editor and `ClaudeModelSelectListFactory` obtains models from the configured Claude endpoint.
- Model selectors remain unavailable until the tenant has valid API-key configuration.
- Select a Claude model and reasoning effort per AI Profile, profile-source template, or Chat Interaction as needed.
- Valid effort choices are `Default`, `Low`, `Medium`, and `High`.
- Applying a profile-source template copies saved Claude session settings to the created profile.
- Grant `ManageClaudeSettings` only to roles that may change tenant Claude credentials and defaults.
- Install CrestApps packages in the web/startup project.

### Feature Overview

| Feature | Feature ID | Purpose |
|---|---|---|
| AI Services | `CrestApps.OrchardCore.AI` | Shared profile, deployment, and orchestration infrastructure |
| AI Claude Orchestrator | `CrestApps.OrchardCore.AI.Chat.Claude` | Claude orchestration, settings, and profile editors |
| AI Chat | `CrestApps.OrchardCore.AI.Chat` | Profile chat user interface when an in-site chat UI is required |
| AI Chat Interactions | `CrestApps.OrchardCore.AI.Chat.Interactions` | Ad-hoc chat interaction editor and hub |

### Install and Enable

Install the orchestrator package in the web/startup project:

```shell
dotnet add package CrestApps.OrchardCore.AI.Chat.Claude
```

Enable the core and Claude features. Add a chat UI feature only when the application needs it:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Claude",
        "CrestApps.OrchardCore.AI.Chat"
      ],
      "disable": []
    }
  ]
}
```

### Configure Shell Defaults

Provide non-secret defaults through the tenant shell configuration. Use user secrets, environment variables, or a secret store for the API key.

```json
{
  "CrestApps": {
    "AI": {
      "Claude": {
        "BaseUrl": "https://api.anthropic.com",
        "DefaultModel": "claude-sonnet-4-5"
      }
    }
  }
}
```

The configuration key is `CrestApps:AI:Claude`. This module binds `ClaudeOptions` from that section before it reads tenant site settings.

### Configure Tenant Settings

1. Enable **AI Claude Orchestrator**.
2. Go to **Settings → Artificial Intelligence → Claude**.
3. Set authentication to **API Key**.
4. Enter the Anthropic API key, base URL, and default model.
5. Save the settings. The editor protects the API key before it is persisted.
6. Reopen the profile or interaction editor and select a discovered Claude model.

`ClaudeOptionsConfiguration` uses a data-protection purpose specific to Claude settings to unprotect the saved key for runtime use. A decryption failure logs a warning and leaves the key unavailable instead of exposing the secret.

### Select Claude for an AI Profile

1. Create or edit an AI Profile.
2. Select the Claude orchestrator.
3. Choose a Claude model and effort level.
4. Configure the profile system message and capabilities normally.
5. Save and test the profile with a user authorized to query it.

Use Claude session settings for per-profile behavior. Do not create an `AIProviderConnection` or an `AIDeployment` merely to select the Claude orchestration runtime.

### Reuse Claude Settings with Templates

Use a profile-source AI Profile Template when multiple profiles should start with the same Claude model and reasoning effort:

1. Create a template whose source is **Profile**.
2. Select the Claude orchestrator.
3. Choose the model and effort level.
4. Create profiles from that template.

`AIProfileTemplateClaudeDisplayDriver` stores the template settings, and the template-to-profile flow propagates them to the generated profile.

### Configure Chat Interactions

Enable `CrestApps.OrchardCore.AI.Chat.Interactions` when administrators need ad-hoc interactions rather than predefined profiles:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Claude",
        "CrestApps.OrchardCore.AI.Chat.Interactions"
      ],
      "disable": []
    }
  ]
}
```

In the interaction editor, select Claude, then select an available model and effort level. `ChatInteractionClaudeDisplayDriver` persists those orchestrator-specific values without changing the generic interaction infrastructure.

### Permission Management

The module registers `ClaudePermissionProvider` and the `ManageClaudeSettings` permission. Administrators receive it by default.

Assign the permission through a role only to trusted operators:

1. Go to **Security → Roles**.
2. Edit the operations role.
3. Grant **Manage Claude Settings**.
4. Do not grant the permission to ordinary profile authors unless they must manage tenant credentials.

### Key Implementation Types

| Type | Responsibility |
|---|---|
| `ClaudeOptionsConfiguration` | Binds shell options, overlays site settings, and unprotects the API key |
| `ClaudeSettingsDisplayDriver` | Renders and updates tenant Claude settings |
| `ClaudeModelSelectListFactory` | Builds model choices from the configured Claude service |
| `ClaudeOrchestratorAvailabilityProvider` | Reports whether Claude can be selected |
| `AIProfileClaudeDisplayDriver` | Adds Claude settings to an AI Profile editor |
| `AIProfileTemplateClaudeDisplayDriver` | Adds Claude settings to a profile-template editor |
| `ChatInteractionClaudeDisplayDriver` | Adds Claude settings to a Chat Interaction editor |
| `ClaudePermissionProvider` | Provides `ManageClaudeSettings` |

### Security Checklist

- Keep API keys outside recipes and checked-in configuration.
- Use `dotnet user-secrets` locally and environment variables, Key Vault, or an equivalent secret provider in production.
- Give `ManageClaudeSettings` only to trusted tenant administrators.
- Use an intentional default model and per-item model overrides to manage availability and cost.
- Limit access to the selected AI Profiles separately through Orchard Core AI profile permissions.
