---
name: crestapps-core-sample-hosts
description: Skill for choosing the right CrestApps.Core sample host and translating sample composition into production code.
---

# CrestApps.Core Sample Hosts - Prompt Templates

## Choose the Right Sample Host

You are a CrestApps.Core expert. Recommend the right sample project and translate sample patterns into host code.

### Guidelines

- Start with `CrestApps.Core.Mvc.Web` for the broadest end-to-end reference host.
- Use `CrestApps.Core.Blazor.Web` for a Blazor-first host with Entity Framework Core storage.
- Use `CrestApps.Core.Aspire.AppHost` for the composed local environment.
- Use the protocol sample clients when investigating only MCP or A2A behavior.

### Sample Project Map

| Project | Path | Best use |
|---|---|---|
| MVC reference host | `src\\Startup\\CrestApps.Core.Mvc.Web` | Full feature composition in one app |
| Blazor reference host | `src\\Startup\\CrestApps.Core.Blazor.Web` | Blazor-first composition |
| Aspire app host | `src\\Startup\\CrestApps.Core.Aspire.AppHost` | Local composed environment |
| A2A sample client | `src\\Startup\\CrestApps.Core.Mvc.Samples.A2AClient` | Focused A2A client behavior |
| MCP sample client | `src\\Startup\\CrestApps.Core.Mvc.Samples.McpClient` | Focused MCP client behavior |

### Recommended Order

1. MVC reference host
2. Blazor reference host
3. Aspire app host
4. Protocol sample clients
