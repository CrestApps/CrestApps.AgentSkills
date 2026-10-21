---
name: orchardcore-ai-profile-builder
description: Skill for the AI Profile Builder in Orchard Core with the CrestApps AI Chat Interactions module. Covers the CrestApps.OrchardCore.AI.Chat.Interactions.ProfileBuilder feature that builds an AI profile by role-play, the Feedback, regenerate and Looks good actions, the coach deployment and the profile-builder-coach and profile-builder-replay-judge templates, revisions, replay with Pass, Fail and Unknown verdicts, saving as a new AI profile or updating the source profile, voice role-play, the ProfileBuilderSession ledger and ProfileBuilderSessionIndex, the AIProfileBuilderSessions admin list, the ManageAIProfiles and EditChatInteractions permissions, and the ProfileBuilderCoaching and ProfileBuilderReplay usage purposes. Use this skill when requests mention Orchard Core Profile Builder, building an AI profile from feedback, or tuning a system message from conversations. Strong matches include ProfileBuilderController, ProfileBuilderService, ProfileBuilderCoach, and ProfileBuilderInteractionMetadata.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Profile Builder - Prompt Templates

## Build AI Profiles by Role-Play

You are an Orchard Core expert. Help set up, use and troubleshoot the CrestApps AI Profile Builder, which turns feedback on role-played conversations into an AI profile's system message and parameters.

### Guidelines

- Enable `CrestApps.OrchardCore.AI.Chat.Interactions.ProfileBuilder` (**AI Profile Builder**). It ships in the `CrestApps.OrchardCore.AI.Chat.Interactions` package and depends on `CrestApps.OrchardCore.AI.Chat.Interactions`.
- A user needs both `ManageAIProfiles` and `EditChatInteractions`. The menu item **Artificial Intelligence → Profile Builder** is guarded by `ManageAIProfiles`.
- The builder suits conversation-driven profiles, such as automated SMS conversations or phone calls, whose behavior depends on the system message.
- A builder session is a chat interaction, so role-play gets everything an interaction has: streaming, tools, data sources, documents, the speech-to-text plus text-to-speech conversation mode and realtime voice. See `orchardcore-ai-chat-interactions`.
- Choose a capable **Coach deployment**: it rewrites the instructions. By default it is the session's chat deployment.
- Documents attached to the session are not copied to the profile.
- Recipe JSON uses `{ "steps": [...] }` and C# classes are `sealed`, except view models.

### Enable

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Chat.Interactions.ProfileBuilder",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

A chat deployment that declares `textGeneration` must exist. See `orchardcore-ai-model-capabilities`.

### The Loop

1. **New Session** starts empty; **Improve an existing AI profile...** starts from a profile's instructions and settings and can later update that profile.
2. Pick the **Chat deployment** in **Settings** and, optionally, a first system message.
3. Type what the person the profile serves would say.
4. Under a reply, click **Feedback** and describe what was wrong:
   - **Feedback & regenerate** (latest reply only) rewrites the instructions, deletes the reply and the message it answered, and sends that message again so the new reply has no trace of the rejected one.
   - **Feedback only** rewrites the instructions and keeps the reply.
5. Click **Looks good** on replies to keep; the coach preserves that behavior in later rewrites.
6. **Revisions** lists every version of the instructions with the coach's summary; **Restore** makes one current. Manual edits in **Settings** become revisions too.
7. **Replay** answers every message that received feedback again and judges each answer.
8. **Finish** saves the result.

### What the Coach Receives and Returns

The coach's system prompt is the `profile-builder-coach` template in the module's `Templates/Prompts` folder. Each request carries the current system message, opening message and parameters, the conversation so far, every earlier piece of feedback (oldest first, with each rejected or approved reply quoted, including replies regenerated away), the revision history, and the new feedback. It returns JSON with the complete system message and, when needed, new values for the opening message, temperature, top-p, frequency and presence penalties, maximum tokens and past message count. Values outside the ranges the settings panel accepts are clamped.

Because every request includes the whole history, a fix found on a later attempt keeps the earlier fixes.

### Replay

Feedback is grouped by situation (the same user message at the same point in a conversation). Up to 20 of the most recent situations are answered again with the current settings, and the `profile-builder-replay-judge` template marks each **Pass**, **Fail** or **Unknown** (shown as **Unclear**). Tools are not offered during a replay, so nothing is sent or booked twice.

### Voice Role-Play

With a conversation deployment, role-play can be spoken and spoken replies can receive feedback. They cannot be regenerated, because they were already heard. A realtime session keeps the instructions it started with; end and restart it to use new ones. See `orchardcore-ai-realtime-voice`.

### Saving the Result

| Action | Result |
|---|---|
| **Create AI profile** | New chat profile with the system message, opening message (`AIProfileMetadata.InitialPrompt`), parameters, chat and utility deployments, orchestrator, tools, agents, MCP and A2A connections, tool instances, data source and retrieval settings, model parameters and prompt templates |
| **Update the AI profile** | Writes the same values onto the profile the session started from or last saved to |
| **Copy system message** | Copies the current instructions |

A conversation deployment on the session sets the new profile's chat mode to `Conversation` with the same conversation deployment and voice.

### How It Is Built

| Piece | Detail |
|---|---|
| Conversation | A `ChatInteraction` marked with `ProfileBuilderInteractionMetadata`; the chat interaction hub is used unchanged |
| Ledger | A `ProfileBuilderSession` document in the `AI` collection, indexed by `ProfileBuilderSessionIndex`, holding feedback, revisions and the last replay. It is kept apart from the interaction so a settings autosave cannot overwrite it |
| Page | The chat interaction editor with a `ChatInteractionChatClientOverrides` request feature that adds **Feedback** and **Looks good** under each reply; `profile-builder.js` (resource `ProfileBuilderApp`) drives the chat client |
| Services | `ProfileBuilderService`, `ProfileBuilderCoach`, `IProfileBuilderSessionStore` |
| Routes | `ai/profile-builder` (list), and `ai/profile-builder/{action}/{id?}` for `New`, `Session`, `State`, `Feedback`, `Restore`, `Replay`, `Settings`, `SaveProfile` and `Delete` |
| Admin list | Orchard Core `AdminList` named `AIProfileBuilderSessions` (rows `ProfileBuilderSessionEntry_SummaryAdmin`; columns `Name`, `Activity`, `Modified`, `Actions`). Builder sessions are left out of the Chat Interactions list |

### Usage Analytics

Coach calls are recorded under the usage purpose `ProfileBuilderCoaching` and replay and judge calls under `ProfileBuilderReplay` (`AIUsageFeaturePurposes`), so they can be told apart from conversation traffic. See `orchardcore-ai-usage-analytics`.

### Troubleshooting

- **No Profile Builder menu**: the user lacks `ManageAIProfiles`, or the feature is off.
- **Session opens but feedback fails**: the user also needs `EditChatInteractions` on the interaction.
- **New instructions ignored in a voice session**: end and restart the realtime session.
- **Replay never calls a tool**: expected; tools are withheld during replay.
