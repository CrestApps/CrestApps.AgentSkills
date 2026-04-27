---
name: orchardcore-ai-chat-analytics
description: Skill for configuring AI Chat Session Analytics in Orchard Core with CrestApps modules. Covers session metrics tracking (unique visitors, handle time, containment rate, abandonment rate), dashboard reports (overview, time-of-day, day-of-week, user segments, performance, conversion, feedback), date range and profile filtering, CSV export, and per-profile analytics settings including conversion goals. Use it for CrestApps.OrchardCore.AI.Chat.Analytics, usage analytics, and related Orchard Core setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Analytics - Prompt Templates

## Configure AI Chat Analytics

You are an Orchard Core expert. Generate code, configuration, and recipes for adding AI chat session analytics and reporting to an Orchard Core application using CrestApps modules.

### Guidelines

- The AI Chat Analytics feature (`CrestApps.OrchardCore.AI.Chat.Analytics`) tracks chat session metrics and provides dashboard reporting with extensible display drivers.
- It depends on both the AI Chat feature (`CrestApps.OrchardCore.AI.Chat`) and the AI Chat Core feature (`CrestApps.OrchardCore.AI.Chat.Core`).
- Metrics are collected automatically when analytics is enabled on an AI profile.
- Per-profile analytics settings control whether session metrics, AI resolution detection, and conversion metrics are active.
- Dashboard reports are rendered as display drivers on `AIChatAnalyticsReport`, making them extensible through Orchard Core's display management.
- Filters are rendered as display drivers on `AIChatAnalyticsFilter`, supporting date range and profile filtering.
- Session data is stored as `AIChatSessionEvent` documents using YesSql with the `AIChatSessionMetricsIndex` for efficient queries.
- Install CrestApps packages in the web/startup project.

### Feature ID

| Feature | ID | Category |
|---------|------|----------|
| AI Chat Session Analytics | `CrestApps.OrchardCore.AI.Chat.Analytics` | Artificial Intelligence |

### Dependencies

| Dependency | Feature ID |
|------------|-----------|
| AI Chat | `CrestApps.OrchardCore.AI.Chat` |
| AI Chat Core | `CrestApps.OrchardCore.AI.Chat.Core` |

### NuGet Package

Install `CrestApps.OrchardCore.AI.Chat` in the web/startup project. The analytics feature is included in this module.

### Enabling AI Chat Analytics

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Chat.Analytics",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Metrics Tracked

The analytics feature tracks the following metrics per chat session:

| Metric | Description |
|--------|-------------|
| Session ID | Unique identifier for each chat session |
| Profile ID | The AI profile used for the session |
| Visitor ID | Unique visitor identifier (up to 64 characters) |
| User ID | Authenticated user identifier (if applicable) |
| Session start/end timestamps | When the session started and ended (UTC) |
| Message count | Total messages exchanged in the session |
| Handle time (seconds) | Duration of the chat session |
| Is resolved | Whether the session was resolved by AI |
| Input/output tokens | Total tokens consumed and generated |
| Average response latency (ms) | Mean AI response time across completions |
| Completion count | Number of AI completions in the session |
| Conversion score | Numeric score for conversion goal tracking |
| Thumbs up/down counts | User feedback on assistant messages |

### Dashboard Reports

The analytics dashboard at **Admin → Artificial Intelligence → Reports → AI Chat Session Analytics** provides seven report sections, each rendered by its own display driver:

| Report | Shape Name | Description |
|--------|-----------|-------------|
| Overview | `ChatAnalyticsOverview` | KPI cards showing total sessions, unique visitors, average handle time, containment rate %, abandonment rate %, resolved/abandoned sessions, returning user rate %, and average messages per session |
| Time-of-Day | `ChatAnalyticsTimeOfDay` | Hourly session distribution (0–23) |
| Day-of-Week | `ChatAnalyticsDayOfWeek` | Daily session distribution across the week |
| User Segments | `ChatAnalyticsUserSegment` | Breakdown of authenticated vs. anonymous sessions and unique users |
| Performance | `ChatAnalyticsPerformance` | Average response latency, total and average input/output tokens per session |
| Conversion | `ChatAnalyticsConversion` | AI resolution rate %, conversion score %, high-performing and low-performing session counts |
| Feedback | `ChatAnalyticsFeedback` | Thumbs up/down counts, percentages, and overall feedback rate |

### Filtering Reports

Reports support two filters rendered as display drivers on `AIChatAnalyticsFilter`:

| Filter | Shape Name | Description |
|--------|-----------|-------------|
| Date Range | `ChatAnalyticsDateRangeFilter_Edit` | Filter by start and end date (UTC) |
| Profile | `ChatAnalyticsProfileFilter_Edit` | Filter by AI profile from a dropdown |

### CSV Export

The dashboard includes an **Export** action that generates a CSV file with session-level data including SessionId, ProfileId, VisitorId, UserId, IsAuthenticated, SessionStartedUtc, SessionEndedUtc, MessageCount, HandleTimeSeconds, and IsResolved.

### Permissions

| Permission | Description |
|------------|-------------|
| `ViewChatAnalytics` | View AI Chat Analytics dashboard |
| `ExportChatAnalytics` | Export AI Chat Analytics data to CSV |

Both permissions are granted to the Administrator role by default.

### Admin Menu Entries

Enabling the analytics feature adds four entries under **Artificial Intelligence → Reports**:

1. AI Chat Session Analytics
2. AI Chat Extracted Data
3. AI Usage Analytics
4. AI Chat Conversion Goals

### Per-Profile Analytics Settings

Analytics behavior is configured on each AI profile through the `AnalyticsMetadata` property:

| Setting | Description |
|---------|-------------|
| Enable Session Metrics | Track session-level metrics (visitors, handle time, message count) |
| Enable AI Resolution Detection | Automatically detect whether AI resolved the user query |
| Enable Conversion Metrics | Track conversion goals defined on the profile |

### Configuring Analytics on a Profile via Code

```csharp
public sealed class AnalyticsProfileMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public AnalyticsProfileMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.NewAsync();

        profile.Name = "support-chat";
        profile.DisplayText = "Support Chat";
        profile.Type = AIProfileType.Chat;

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "You are a helpful support assistant.",
            Temperature = 0.3f,
            MaxTokens = 4096,
            PastMessagesCount = 10,
        });

        profile.Put(new AnalyticsMetadata
        {
            EnableSessionMetrics = true,
            EnableAIResolutionDetection = true,
            EnableConversionMetrics = true,
            ConversionGoals =
            [
                new ConversionGoal
                {
                    Name = "issue-resolved",
                    Description = "The user's issue was fully resolved by the assistant.",
                    MinScore = 0,
                    MaxScore = 100,
                },
            ],
        });

        await _profileManager.SaveAsync(profile);

        return 1;
    }
}
```

### Key Services

| Service | Description |
|---------|-------------|
| `AIChatSessionEventService` | Core persistence service for recording session events (start, end, latency, tokens, resolution, conversion, ratings) |
| `AICompletionUsageService` | Aggregates token usage analytics across sessions |
| `AnalyticsChatSessionHandler` | `IAIChatSessionHandler` that records session start on first user message and tracks response latency |
| `AIChatSessionEventPostCloseObserver` | Records session end with resolution status and conversion goal results |

### Extending Reports with Custom Display Drivers

Reports are extensible through Orchard Core's display driver pattern. Register a custom display driver on `AIChatAnalyticsReport` to add a new report section:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIChatAnalyticsReport, CustomAnalyticsDisplayDriver>();
    }
}
```

### Extending Filters with Custom Display Drivers

Add custom filters by registering a display driver on `AIChatAnalyticsFilter`:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIChatAnalyticsFilter, CustomAnalyticsFilterDisplayDriver>();
    }
}
```

The custom filter driver can append query conditions to `AIChatAnalyticsFilter.Conditions` to restrict the result set.

### Usage Analytics

The **AI Usage Analytics** page (separate from session analytics) shows aggregated token usage data grouped by user, client, and model. It displays total API calls, sessions, chat interactions, and tokens with average latency per group.
