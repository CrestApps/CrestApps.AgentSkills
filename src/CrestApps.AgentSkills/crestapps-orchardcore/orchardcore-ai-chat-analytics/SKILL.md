---
name: orchardcore-ai-chat-analytics
description: Skill for configuring AI Chat Session Analytics in Orchard Core with CrestApps modules. Covers the CrestApps.OrchardCore.AI.Chat.Analytics feature, per-profile AnalyticsMetadata (session metrics, AI resolution detection, conversion goals), the AI Chat Session Analytics, Extracted Data and Conversion Goals reports under Reports, the date range selector, CSV export, feedback ratings, inactivity timeout, data extraction and post-session processing tasks, the queryChatSessionMetrics tool, chat report builder data sources, and extending the dashboard with AIChatAnalyticsReport and AIChatAnalyticsFilter display drivers. Use it for chat analytics, containment rate, abandonment rate, conversion scoring, post-session dispositions, and related setup or troubleshooting work. Strong matches include ViewChatAnalytics, ExportChatAnalytics, AIChatSessionEvent, AIChatSessionMetricsIndex, AIProfilePostSessionSettings, and AIProfileDataExtractionSettings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Analytics - Prompt Templates

## Configure AI Chat Analytics

You are an Orchard Core expert. Generate code, configuration, and recipes for AI chat session analytics and reporting with the CrestApps modules.

### Guidelines

- `CrestApps.OrchardCore.AI.Chat.Analytics` ships in the `CrestApps.OrchardCore.AI.Chat` package and depends on `CrestApps.OrchardCore.AI.Chat` and `CrestApps.OrchardCore.AI.Chat.Core`.
- Session metrics are **off by default**. Turn on **Enable session metrics** per chat profile (`AnalyticsMetadata.EnableSessionMetrics`) on the **Data Processing & Metrics** tab. The analytics fields only appear on **Chat** profiles.
- The three reports sit under **Reports → Artificial Intelligence Reports** and use the same date range selector as other reports (relative ranges such as *Last 30 Days*, or custom dates) plus an optional profile filter.
- **AI Usage Analytics** is not part of this feature. It needs only AI Chat, is listed under **Reports → Billing & Usage**, and is covered by `orchardcore-ai-usage-analytics`.
- Dashboard sections are display drivers on `AIChatAnalyticsReport`; filters are display drivers on `AIChatAnalyticsFilter`.
- Session data is stored as `AIChatSessionEvent` documents indexed by `AIChatSessionMetricsIndex`.
- Install CrestApps packages in the web/startup project. Recipe JSON uses `{ "steps": [...] }` and C# classes are `sealed`, except view models.

### Enable Analytics

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Chat.Analytics",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Reports and Permissions

| Report | Route | Permission |
|---|---|---|
| AI Chat Session Analytics | `AI/ChatAnalytics/Index` | `ViewChatAnalytics` |
| AI Chat Extracted Data | `AI/ChatExtractedData/Index` | `ViewChatAnalytics` |
| AI Chat Conversion Goals | `AI/ChatConversionGoals/Index` | `ViewChatAnalytics` |
| CSV export from these reports | | `ExportChatAnalytics` |

Both permissions are granted to the Administrator role. The menu entries are merged by the Reports module into the **Artificial Intelligence Reports** category.

### Dashboard Sections

| Section | Shape | Shows |
|---|---|---|
| Overview | `ChatAnalyticsOverview` | Sessions, unique visitors, containment and abandonment rate, average duration, messages per session, returning users, steps to resolve |
| Time of day | `ChatAnalyticsTimeOfDay` | Hourly distribution |
| Day of week | `ChatAnalyticsDayOfWeek` | Daily distribution (localizable day names) |
| User segments | `ChatAnalyticsUserSegment` | Authenticated versus anonymous sessions and visitors |
| Performance | `ChatAnalyticsPerformance` | Latency and input or output tokens |
| Conversion | `ChatAnalyticsConversion` | Resolution rate, conversion score, high and low performers |
| Feedback | `ChatAnalyticsFeedback` | Thumbs up and down, satisfaction and feedback rate |

Filters: `ChatAnalyticsDateRangeFilter_Edit` and `ChatAnalyticsProfileFilter_Edit`. The CSV export holds `SessionId`, `ProfileId`, `VisitorId`, `UserId`, `IsAuthenticated`, `SessionStartedUtc`, `SessionEndedUtc`, `MessageCount`, `HandleTimeSeconds` and `IsResolved`.

### Per-Profile Analytics

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "support-chat",
          "DisplayText": "Support Chat",
          "Type": "Chat",
          "Properties": {
            "AnalyticsMetadata": {
              "EnableSessionMetrics": true,
              "EnableAIResolutionDetection": true,
              "EnableConversionMetrics": true,
              "ConversionGoals": [
                {
                  "Name": "issue_resolution",
                  "Description": "Rate whether the user's primary issue was fully resolved.",
                  "MinScore": 0,
                  "MaxScore": 10
                }
              ]
            }
          },
          "Settings": {
            "AIProfileDataExtractionSettings": {
              "SessionInactivityTimeoutInMinutes": 30
            }
          }
        }
      ]
    }
  ]
}
```

- `EnableAIResolutionDetection` defaults to `true`. It lets an AI review a closed session and mark it resolved even when the user simply stopped chatting.
- Goal names use letters, digits and underscores; `Description` is required; `MinScore` defaults to `0` and `MaxScore` to `10`, and the maximum must exceed the minimum.
- Sessions idle past the inactivity timeout (default 30 minutes) are closed as abandoned by a background task that runs every 10 minutes.
- When session metrics are on, thumbs up and down appear on assistant messages in the admin chat, the admin widget and the frontend widget. The rating is per session.

In code:

```csharp
profile.Put(new AnalyticsMetadata
{
    EnableSessionMetrics = true,
    EnableAIResolutionDetection = true,
    EnableConversionMetrics = true,
    ConversionGoals =
    [
        new ConversionGoal
        {
            Name = "issue_resolution",
            Description = "Rate whether the user's primary issue was fully resolved.",
            MinScore = 0,
            MaxScore = 10,
        },
    ],
});
```

### Data Extraction and Post-Session Processing

Both are on the same **Data Processing & Metrics** tab and come with AI Chat.

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "support-chat",
          "Settings": {
            "AIProfileDataExtractionSettings": {
              "EnableDataExtraction": true,
              "DataExtractionEntries": [
                { "Name": "customer_email", "Description": "The customer's email address.", "AllowMultipleValues": false, "IsUpdatable": true }
              ]
            },
            "AIProfilePostSessionSettings": {
              "EnablePostSessionProcessing": true,
              "PostSessionTasks": [
                {
                  "Name": "disposition",
                  "Type": "PredefinedOptions",
                  "Instructions": "Classify the outcome of this conversation.",
                  "AllowMultipleValues": false,
                  "Options": [
                    { "Value": "Resolved", "Description": "The issue was fully addressed." },
                    { "Value": "Escalated", "Description": "The user asked for a human." }
                  ]
                },
                {
                  "Name": "summary",
                  "Type": "Semantic",
                  "Instructions": "Write a 2-3 sentence summary of the conversation."
                }
              ]
            }
          }
        }
      ]
    }
  ]
}
```

- Task names are unique and use letters, digits and underscores. A `PredefinedOptions` task needs at least one option with unique values, and enabling processing needs at least one task.
- Each task is tried up to 3 times; malformed or truncated structured output counts as a failed attempt. When every task is `Succeeded` or `Failed`, the **AI Chat Session Post-Processed** workflow event fires (see `orchardcore-ai-workflows`).
- Tools selected on the post-session **Capabilities** tab (`ToolNames`) are available only during post-session processing.

### AI Tools

With `CrestApps.OrchardCore.AI.Agent` also enabled, the tools `queryChatSessionMetrics` (optional `profileId`, `startDateUtc`, `endDateUtc`), `listAIProfiles` and `viewAIProfile` let a profile query analytics. Combine them with `generate_chart` for charts.

### Report Builder Data Sources

When `OrchardCore.DataPipelines.Core` is enabled, AI Chat registers the **AI chat** data source (`AIChat`) with a **Chat sessions** data set, and this feature adds a **Chat session metrics** data set (one row per tracked session with usage, feedback and conversion metrics), so designed reports can read chat data. See `orchardcore-report-builder`.

### Extend the Dashboard

```csharp
public sealed class EscalationRateDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    public override IDisplayResult Display(AIChatAnalyticsReport report, BuildDisplayContext context)
    {
        return Initialize<EscalationRateViewModel>("ChatAnalyticsEscalationRate", model =>
        {
            model.Escalated = report.Events.Count(e => !e.IsResolved && e.SessionEndedUtc.HasValue);
        }).Location("Content:10");
    }
}

public class EscalationRateViewModel
{
    public int Escalated { get; set; }
}

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIChatAnalyticsReport, EscalationRateDisplayDriver>();
    }
}
```

Add a `ChatAnalyticsEscalationRate.cshtml` view. A custom filter is a `DisplayDriver<AIChatAnalyticsFilter>` that appends to `AIChatAnalyticsFilter.Conditions` (a list of `Func<IQuery<AIChatSessionEvent>, IQuery<AIChatSessionEvent>>`). `AIChatAnalyticsReport` exposes `Events` and `Filter`.

### Key Services (CrestApps.Core)

| Service | Role |
|---|---|
| `IAIChatSessionEventService` | Stores session events (`DefaultAIChatSessionEventService`) |
| `DefaultAIChatSessionAnalyticsHandler` | Records analytics on session events |
| `AIChatSessionPostCloseProcessor` | Resolution, conversion and post-session tasks after close |
| `IAICompletionUsageService` | Completion usage records used by usage analytics |

### Upgrading from 2.x

- The reports moved from **Artificial Intelligence → Reports** to **Reports → Artificial Intelligence Reports**, and **AI Usage Analytics** moved to **Reports → Billing & Usage**.
- Separate **From** and **To** fields became the shared date range selector. A custom filter driver should read `AIChatAnalyticsFilter.StartDateUtc` and `EndDateUtc`.
- Permission descriptions use `LocalizationSource`; the permission names are unchanged.
