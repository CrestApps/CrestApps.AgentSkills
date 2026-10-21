---
name: orchardcore-ai-tools
description: Skill for registering, authorizing and selecting AI tools (functions) in Orchard Core with CrestApps AI Services (CrestApps.OrchardCore.AI). Covers AddCoreAITool and the AIToolBuilder (WithTitle, WithDescription, WithCategory, WithPurpose, WithDependency, Selectable, Hidden), system versus hidden versus selectable tools, AIToolDefinitionOptions.GetSelectableTools, how the orchestrator assembles tools from ProfileToolRegistryProvider, SystemToolRegistryProvider, tool instances and agents, where selections are stored (FunctionInvocationMetadata, ProfileTemplateMetadata.ToolNames, ChatInteraction.ToolNames), the AccessAnyAITool and AccessAITool_{toolName} permissions with OrchardCoreAIToolAccessEvaluator, when access is checked for editors, chat interactions and AI profile sessions, and MCP exposure of selectable tools. Use this skill when requests mention Orchard Core AI tools, custom AI functions, AddCoreAITool, selectable or system tools, tool permissions, or tools missing for a role.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Tools

A tool is a named `AITool` (usually an `AIFunction`) registered in code. Profiles, templates, chat interactions, and workflow tasks select tools by name; the orchestrator resolves them as keyed services for each request, after an authorization check.

## Guidelines

- Enable `CrestApps.OrchardCore.AI` for the tool pickers, permissions, and access evaluator. Install CrestApps packages in the web/startup project.
- Register a tool with `services.AddCoreAITool<TTool>(name)`; it adds the definition to `AIToolDefinitionOptions` and a keyed `AITool` singleton under the same name. Names must be unique and stable.
- **A new tool is a system tool by default.** System tools are auto-included by the orchestrator, never appear in pickers, and are never exposed by the MCP server. Call `.Selectable()` to show it in pickers, or `.Hidden()` to keep it out of pickers while letting a profile (for example a system agent) reference it by name.
- Only selectable tools can be opted in for MCP clients (`orchardcore-ai-mcp-server`).
- Descriptions are what the model reads to choose a tool. Describe side effects and required arguments precisely.
- Do not make destructive or privileged tools selectable for ordinary users.

## Register a custom tool

```csharp
using System.Text.Json;
using CrestApps.Core.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace MyModule;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<LookupOrderTool>(LookupOrderTool.TheName)
            .WithTitle(S["Order Lookup"])
            .WithDescription(S["Looks up a customer's order status by order number."])
            .WithCategory(S["Commerce"])
            .Selectable();
    }
}

public sealed class LookupOrderTool : AIFunction
{
    public const string TheName = "lookupOrder";

    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "orderNumber": { "type": "string", "description": "The order number." }
          },
          "required": ["orderNumber"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Looks up a customer's order status by order number.";

    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetValue("orderNumber", out var value) || value is null)
        {
            return ValueTask.FromResult<object>("The orderNumber argument is required.");
        }

        // Resolve scoped services from arguments.Services and check permissions before reading data.
        return ValueTask.FromResult<object>($"Order {value} is in transit.");
    }
}
```

The tool is a singleton. Resolve tenant services per call from `arguments.Services`, never through constructor-injected scoped services.

### Builder methods

| Method | Effect |
|---|---|
| `WithTitle`, `WithDescription`, `WithCategory` | Picker text and grouping (title and description default to the name) |
| `WithPurpose` | Purpose tag the orchestrator can use to discover tools (see `AIToolPurposes`) |
| `WithDependency`, `WithDependencies` | Tools automatically included when this tool is selected |
| `WithoutDependency`, `WithoutDependencies` | Remove a dependency |
| `Selectable()` | Not a system tool, shown in pickers |
| `Hidden()` | Not a system tool, not shown in pickers, still selectable by name |

`AIToolDefinitionOptions.GetSelectableTools()` returns the tools that are neither system nor hidden.

## How the tool set is built

For every completion, the orchestrator asks its registry providers for tools:

1. `ProfileToolRegistryProvider` returns the tool names configured on the completion context, skipping system tools.
2. `SystemToolRegistryProvider` adds the system tools.
3. Tool instances (`orchardcore-ai-tool-instances`), agents (`orchardcore-ai-agent`), MCP connections, and A2A connections contribute their own entries.
4. Each selected tool's dependencies are added.
5. The tools are materialized and added to the request. Access checks use `IAIToolAccessEvaluator`; Orchard Core registers `OrchardCoreAIToolAccessEvaluator`, which authorizes `AccessAITool` with the tool name as the resource.

When the checks run:

| Moment | Check |
|---|---|
| Editing a profile, template, interaction, workflow task, or the MCP Server card | Pickers list only the selectable tools the editing user may access. |
| Sending a **chat interaction** message | Each selectable tool on the interaction is re-checked for the caller. Denied tools are **removed from the request, not failed**, and a `Warning` log entry lists them. |
| Running an **AI profile** session | The profile is the authorization boundary: it runs exactly as configured (it may be anonymous), with no per-user tool check at send time. Control who may use it with the profile query permissions. |
| System and hidden (dependency) tools | Never checked. |

## Where selections are stored

| Surface | Editor | Storage |
|---|---|---|
| AI Profile | **Capabilities** tab, **Tools** | `FunctionInvocationMetadata.Names` (the legacy `AIProfileFunctionInvocationMetadata` is read and removed on save) |
| AI Profile Template (Profile source) | **Capabilities** tab | `ProfileTemplateMetadata.ToolNames` |
| Chat Interaction | **Capabilities** tab | `ChatInteraction.ToolNames` |
| AI Completion using Direct Config task | **Capabilities** tab | `ToolNames` of the task's embedded `ChatInteraction` |
| Post-session processing | **Data Processing & Metrics → Capabilities** | Stored apart from the live selection |

Pickers list only the selectable tools the editing user may access, grouped by category, and saving keeps only names the user may access.

## Permissions

| Permission | Meaning |
|---|---|
| `AccessAnyAITool` | Use every tool. Security critical; granted to Administrator by default. |
| `AccessAITool` | Base permission checked with the tool name as resource; implied by the two others. |
| `AccessAITool_{toolName}` | Use one tool. Generated for every registered tool and every tool instance function. |

Who may *configure* a tool and who may *run* it are different questions. Editors need the tool permission to select it. Chat interaction users need it at send time. AI profile sessions rely on the profile query permissions (`QueryAnyAIProfile` or `QueryAIProfile_{profileName}`), so only attach privileged tools to profiles whose query permission is equally restricted.

### How the caller is resolved

The caller comes from the framework's `IUserAccessor`, not `IHttpContextAccessor`. The AI chat hubs publish the connection principal for each invocation, so checks over SignalR match checks over HTTP.

- **No caller** (background task, workflow, recipe): checks are skipped and every configured tool stays available.
- **Anonymous caller** of a checked request: the check runs, so only what the `Anonymous` role is granted applies.

## Built-in tool catalogs

| Feature | Tools |
|---|---|
| `CrestApps.OrchardCore.AI.Agent` | Content, content definition, feature, tenant, recipe, user, role, workflow, communication, AI profile, and analytics tools. See `orchardcore-ai-agent`. |
| `CrestApps.OrchardCore.AI.Memory` | Search, list, save, and remove user memories (authenticated users with memory enabled). See `orchardcore-ai-memory`. |
| `CrestApps.OrchardCore.AI.A2A` | `listAvailableAgents`, `findAgentForTask`, `findToolsForTask`. See `orchardcore-ai-a2a-client`. |
| AI Documents features | Document, tabular, PowerPoint, Word, and PDF tools and system agents. See `orchardcore-ai-documents`. |

Functions an administrator creates from a tool instance source are tenant data, not code registrations; see `orchardcore-ai-tool-instances`.

## Troubleshooting

| Symptom | Check |
|---|---|
| A registered tool is not in the picker | It is a system tool. Add `.Selectable()`. |
| A chat interaction tool works for admins but not for a role | Grant `AccessAITool_{toolName}` (or `AccessAnyAITool`) and look for the `Warning` that lists excluded tools. |
| An editor cannot see a tool in the picker | The editing user lacks `AccessAITool_{toolName}`. |
| A tool is missing for MCP clients | Only selectable tools can be exposed, and they must be opted in on the MCP Server card. |
| A tool resolves stale tenant data | Do not inject scoped services into the singleton tool; resolve them from `arguments.Services`. |
