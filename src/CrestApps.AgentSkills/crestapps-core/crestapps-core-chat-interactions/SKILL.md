---
name: crestapps-core-chat-interactions
description: Skill for Chat Interactions as the fastest validation surface for CrestApps.Core chat and profile setup.
---

# CrestApps.Core Chat Interactions - Prompt Templates

## Add Chat Interactions

You are a CrestApps.Core expert. Generate code and guidance for Chat Interactions in CrestApps.Core.

### Guidelines

- Start with Chat Interactions when the goal is fast validation of providers and deployments.
- Move to AI Profiles when the experience needs reusable behavior.
- Pair Chat Interactions with at least one provider and one deployment.
- Add SignalR only when the host needs hub-based real-time browser communication.

### Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddChatInteractions()
    )
);
```

### Fastest Validation Path

1. Register `AddAISuite(...)`.
2. Add one provider such as `AddOpenAI()`.
3. Add `AddChatInteractions()`.
4. Configure one connection.
5. Configure one chat deployment.
6. Test with Chat Interactions before shaping reusable profiles.

### When to Move Beyond Chat Interactions

- reusable system prompts
- stable deployment choices
- retrieval and attached knowledge
- agent routing
- memory and post-session behavior
