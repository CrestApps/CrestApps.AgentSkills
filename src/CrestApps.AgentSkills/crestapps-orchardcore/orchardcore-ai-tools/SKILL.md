---
name: orchardcore-ai-tools
description: Skill for registering, authorizing, and selecting AI tools in Orchard Core using the CrestApps AI Services modules. Covers AIToolDefinitionOptions, local and system tool registry sources, keyed AITool instances, tool selection on AI profiles, profile templates, chat interactions, and direct-config workflow tasks, plus per-tool permissions. Use this skill when requests mention Orchard Core AI Tools, AIToolDefinitionOptions, AddAITool, selectable tools, AI Profile Capabilities, tool authorization, local tool registry, system tools, or closely related CrestApps implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.AI, LocalToolRegistryProvider, OrchardCoreAIToolAccessEvaluator, AIToolPermissionProvider, AIProfileToolsDisplayDriver, AIProfileTemplateToolsDisplayDriver, ChatInteractionToolsDisplayDriver, AICompletionWithConfigTaskDisplayDriver.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Tools - Prompt Templates

## Register and Select AI Tools

You are an Orchard Core expert. Generate secure AI tool registration and selection patterns for CrestApps AI Services. A tool is a named `AITool` registered in the shared Core tool definitions and resolved as a keyed service when an authorized completion needs it.

### Guidelines
- Enable `CrestApps.OrchardCore.AI` for the Orchard Core AI tool selection and permission integration.
- Register a tool through the shared Core tool registration APIs and give it a unique, stable name.
- Register each tool implementation as a keyed `AITool` service under that same name so registry resolution can create it.
- Supply title, description, and category metadata through `AIToolDefinitionOptions`; these values drive capability pickers.
- `GetSelectableTools()` excludes tools marked as system tools. System tools are for internal orchestration paths and are not shown in capability pickers.
- `LocalToolRegistryProvider` returns only names configured on the completion context, skips system tools, and checks `AccessAITool` authorization.
- The provider resolves a selected tool from DI with `GetKeyedService<AITool>(toolName)`.
- Tool selection is stored as `FunctionInvocationMetadata.Names` for AI Profiles.
- Profile-source templates store selected names in `ProfileTemplateMetadata.ToolNames`.
- Chat Interactions store selected names in `ChatInteraction.ToolNames`.
- Direct-config workflow tasks store selected names in `AICompletionWithConfigTask.ToolNames`.
- Capability editors group tools by category and filter them by the current editor's permission.
- The `AccessAnyAITool` permission is security-critical. Dynamic `AccessAITool_{toolName}` permissions are created for registered definitions.
- Do not make privileged system-management, content-management, or external side-effect tools available by default.
- Tool metadata must accurately describe side effects, required access, and argument expectations so orchestration can select safely.
- Install CrestApps packages in the web/startup project.

### Feature Overview

| Feature | Feature ID | Purpose |
|---|---|---|
| AI Services | `CrestApps.OrchardCore.AI` | Tool capability editors, dynamic permissions, and local registry |
| Orchard Core AI Agent | `CrestApps.OrchardCore.AI.Agent` | Orchard-admin and system tools |
| AI Chat Interactions | `CrestApps.OrchardCore.AI.Chat.Interactions` | Interaction-level tool selection |
| Orchard Core Workflows | `OrchardCore.Workflows` | Direct-config workflow task tool selection |

### Install and Enable

Install AI Services in the web/startup project:

```shell
dotnet add package CrestApps.OrchardCore.AI
```

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Agent"
      ],
      "disable": []
    }
  ]
}
```

Enable `CrestApps.OrchardCore.AI.Agent` only when the site needs its Orchard-management tool catalog. A custom local tool does not require the Agent feature.

### Register a Custom Tool

Register a named tool in the web/startup project or custom Orchard module. The shared Core registration API adds its definition and keyed service:

```csharp
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.DependencyInjection;

namespace MyModule;

public sealed class Startup : OrchardCore.Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<LookupOrderTool>("lookup_order", options =>
        {
            options.Title = "Order Lookup";
            options.Description = "Looks up a customer's order status by order identifier.";
            options.Category = "Commerce";
        });
    }
}
```

Implement the tool using the shared `AITool` contract and a JSON schema that limits arguments. Give the tool a narrow description and perform authorization before accessing tenant data or making an external call.

### Tool Sources and Registry Resolution

The orchestrator can combine registry sources. For local Orchard registrations:

1. A tool registration adds a definition to `AIToolDefinitionOptions`.
2. A profile, interaction, or workflow task supplies selected tool names in its completion context.
3. `LocalToolRegistryProvider` finds matching definitions.
4. It excludes entries where `IsSystemTool` is true.
5. It authorizes the current user against `AIPermissions.AccessAITool` and the tool name resource.
6. It resolves the keyed `AITool` instance and returns a local `ToolRegistryEntry`.

System tools follow a separate system registry path. Do not expect them in profile, template, interaction, workflow-task, or post-session capability pickers.

### Select Tools on an AI Profile

1. Register and authorize the tool.
2. Go to **Artificial Intelligence → Profiles**.
3. Edit the profile and open **Capabilities**.
4. Select tools in the grouped tool list.
5. Save the profile.

`AIProfileToolsDisplayDriver` saves the names in `FunctionInvocationMetadata`. It also reads the legacy `AIProfileFunctionInvocationMetadata` property for compatibility, but new integrations must use the current metadata.

### Select Tools on a Profile Template

Only templates with `Source = Profile` show the tools editor:

1. Create or edit a profile-source template.
2. Select tools under **Capabilities**.
3. Save the template.
4. Create a profile from the template.

`AIProfileTemplateToolsDisplayDriver` stores `ProfileTemplateMetadata.ToolNames`, which can seed the generated profile configuration.

### Select Tools on a Chat Interaction

Enable the interactions feature first:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions"
      ],
      "disable": []
    }
  ]
}
```

`ChatInteractionToolsDisplayDriver` filters selectable tools by the editing user's access and persists the selected names on `ChatInteraction.ToolNames`.

### Use Tools in a Workflow Task

The **AI Completion using Direct Config** task accepts selected local tools. It configures `ChatToolMode.Auto`, resolves each selected name through `IAIToolsService`, and uses function invocation middleware with the configured maximum iterations.

```liquid
{{ Workflow.Output["AI-order-summary"].Content }}
```

Use a direct-config task only for tightly controlled workflow automation. Prefer a dedicated profile when the completion should inherit a reusable profile policy and capability set.

### Tool Permissions

`AIToolPermissionProvider` registers:

| Permission | Purpose |
|---|---|
| `AccessAnyAITool` | Security-critical permission that implies access to registered tools |
| `AccessAITool` | Base tool access permission |
| `AccessAITool_{toolName}` | Dynamic permission for a specific registered tool |

`OrchardCoreAIToolAccessEvaluator` delegates tool decisions to Orchard Core `IAuthorizationService`. Grant access per tool rather than granting `AccessAnyAITool` to ordinary users.

### Security Checklist

- Model every tool as an explicit capability, not as an implicit extension of a profile.
- Use strict JSON schemas and validate all input before an external call or data mutation.
- Require the least-privileged `AccessAITool_{toolName}` permission.
- Keep destructive operations separate from read-only lookup tools.
- Mark internal orchestration-only tools as system tools so administrators cannot select them accidentally.
- Test with both authorized and unauthorized users before exposing a tool on a production profile.
