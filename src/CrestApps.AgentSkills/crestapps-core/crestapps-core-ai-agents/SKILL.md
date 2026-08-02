---
name: crestapps-core-ai-agents
description: Skill for agent profiles, agent availability, and task delegation through the CrestApps.Core orchestrator.
---

# CrestApps.Core AI Agents - Prompt Templates

## Create AI Agents

You are a CrestApps.Core expert. Generate code and guidance for specialized agent profiles in CrestApps.Core.

### Guidelines

- Agents are standard `AIProfile` records with `Type = AIProfileType.Agent`.
- Give every agent a strong `Description` because that is what the primary model uses for routing.
- Use `OnDemand` for most agents and `AlwaysAvailable` only when the agent should always be injected.
- Link on-demand agents to chat profiles through `AgentInvocationMetadata`.
- By default a sub-agent runs with its tools disabled to prevent runaway recursion. Opt in per agent by setting `AgentMetadata.AllowToolInvocation = true`; the agent then runs through the orchestrator with its configured tools enabled, guarded by a recursion-depth limit (`AIInvocationContext.AgentInvocationDepth`) that still blocks an agent from invoking other agents.

### Agent Example

```csharp
var agent = new AIProfile
{
    Type = AIProfileType.Agent,
    Name = "translator",
    DisplayText = "Translator",
    Description = "Translates text between languages.",
    ChatDeploymentName = "gpt-4o-mini",
};

agent.Put(new AgentMetadata
{
    Availability = AgentAvailability.OnDemand,
});

await profileManager.CreateAsync(agent);
```

### Link the Agent to a Chat Profile

```csharp
chatProfile.Put(new AgentInvocationMetadata
{
    Names = ["translator", "code-reviewer"],
});

await profileManager.UpdateAsync(chatProfile);
```

### Allow a Sub-Agent to Use Its Own Tools

By default a delegated agent runs with tools disabled. Enable them explicitly when the agent needs its configured tools during delegation:

```csharp
agent.Put(new AgentMetadata
{
    Availability = AgentAvailability.OnDemand,
    AllowToolInvocation = true,
});
```

Even with `AllowToolInvocation = true`, the recursion-depth guard (`AIInvocationContext.AgentInvocationDepth`) prevents that agent from invoking further agents.

### Availability Modes

| Mode | Use |
|---|---|
| `OnDemand` | Specialized agents assigned per profile |
| `AlwaysAvailable` | Global agents needed in every orchestration request |
