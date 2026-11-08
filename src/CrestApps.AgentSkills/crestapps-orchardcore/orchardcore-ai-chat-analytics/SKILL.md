---
name: orchardcore-ai-chat-analytics
description: Skill for configuring AI Chat Session Analytics in Orchard Core with CrestApps modules. Covers the CrestApps.OrchardCore.AI.Chat.Analytics feature, per-profile AnalyticsMetadata (session metrics, AI resolution detection, conversion goals), the AI Chat Session Analytics, Extracted Data and Conversion Goals reports of the Reports framework, their All Time default range, CSV, Excel and PDF exports gated by ExportChatAnalytics, feedback ratings, inactivity timeout, data extraction and post-session processing tasks, the queryChatSessionMetrics tool, AI chat report builder data sets, and adding custom analytics reports with IAIChatSessionEventReader and AIChatReportFilters. Use it for chat analytics, containment rate, abandonment rate, conversion scoring, post-session dispositions, and related troubleshooting. Strong matches include ViewChatAnalytics, ExportChatAnalytics, AIChatSessionEvent, AIChatSessionMetricsIndex, AIChatSessionAnalyticsReport, AIProfilePostSessionSettings, and AIProfileDataExtractionSettings.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Analytics - Prompt Templates

## Configure AI Chat Analytics

You are an Orchard Core expert. Generate code, configuration, and recipes for AI chat session analytics and reporting with the CrestApps modules.

### Guidelines

- `CrestApps.OrchardCore.AI.Chat.Analytics` (`AIConstants.Feature.ChatAnalytics`) ships in the `CrestApps.OrchardCore.AI.Chat` package and depends on `CrestApps.OrchardCore.AI.Chat`, `CrestApps.OrchardCore.AI.Chat.Core` and `CrestApps.OrchardCore.Reports` (enabled with it).
- Session metrics are **off by default**. Turn on **Enable session metrics** per chat profile (`AnalyticsMetadata.EnableSessionMetrics`) on the **Data Processing & Metrics** tab. The analytics fields only appear on **Chat** profiles.
- The three reports are ordinary reports of the Reports framework (see `orchardcore-reports`) under **Reports → Artificial Intelligence Reports**. They use the shared date range selector (applied to the session start), open on **All Time** (`DefaultDateRange => ReportDefaultDateRange.AllTime`), accept a single bound as "on or after" or "on or before", and add an AI profile filter. Extracted Data and Conversion Goals require a profile.
- **AI Usage Analytics** is not part of this feature. It belongs to the separate **AI Usage Analytics** feature (`CrestApps.OrchardCore.AI.UsageAnalytics`), is listed under **Reports → Billing & Usage**, and is covered by `orchardcore-ai-usage-analytics`.
- The former dashboard display drivers on `AIChatAnalyticsReport` and filters on `AIChatAnalyticsFilter` are gone; add your own figures as another report (see below).
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

| Report | Class | Route | Permission |
|---|---|---|---|
| AI Chat Session Analytics | `AIChatSessionAnalyticsReport` | `/Admin/Reports/view/ai-chat-session-analytics` | `ViewChatAnalytics` |
| AI Chat Extracted Data | `AIChatExtractedDataReport` | `/Admin/Reports/view/ai-chat-extracted-data` | `ViewChatAnalytics` |
| AI Chat Conversion Goals | `AIChatConversionGoalsReport` | `/Admin/Reports/view/ai-chat-conversion-goals` | `ViewChatAnalytics` |
| Export of these three reports | | | `ExportChatAnalytics` (their `IReport.ExportPermission`) |

Both permissions are granted to the Administrator role. Without **Export Chat Analytics** the export buttons are hidden, `ReportsController.Export` refuses, and a report subscription cannot be saved with a file format or attach the file. **View Chat Analytics** alone also runs and exports the AI usage report.

### Dashboard Sections

**AI Chat Session Analytics** shows these sections, each with a description, a tooltip per figure (`ReportMetric.Description`) and an empty-state message saying how to start collecting the data: the overview (sessions, unique visitors, containment and abandonment rate, average duration, messages per session, returning users, steps to resolve), sessions by time of day and by day of week (site time zone, 24-hour clock where the culture uses one), who chats (authenticated versus anonymous), performance (latency and average total, input and output tokens per session), resolution and conversion, and feedback.

Exports (CSV, Excel with Reports (OpenXml), PDF with Reports (PDF)) of these three reports are one flat table, the only section in `ReportRunMode.Export`, with the old invariant CSV columns so parsing scripts keep working:

- Session Analytics: `SessionId,ProfileId,VisitorId,UserId,IsAuthenticated,SessionStartedUtc,SessionEndedUtc,MessageCount,HandleTimeSeconds,IsResolved`.
- Conversion Goals: `SessionStartedUtc,SessionId,TotalPoints`, then a column per goal.
- Extracted Data: `SessionStartedUtc,SessionId`, then a column per extracted field.

A report subscription emails the report as it looks on screen and attaches the export from a second run.

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

When `OrchardCore.DataPipelines.Core` is enabled, AI Chat registers the **AI chat** data source (`AIChat`) with a **Chat sessions** data set, and this feature (`ChatAnalyticsDataSourcesStartup`) adds **Chat session metrics** (one row per tracked session with usage, feedback and conversion metrics), **AI completion usage** (one row per metered AI request, with every billed unit and estimated cost), **AI voice usage** (one row per automated AI voice call summary) and **Conversation evaluations**, all gated by `ViewChatAnalytics`. See `orchardcore-report-builder`.

### Add Your Own Analytics Report

Custom figures are a report of their own on the Reports framework, not a section of these reports:

```csharp
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace MyModule.Reports;

public sealed class LongSessionsReport : IReport, IReportFilterMetadata
{
    private readonly IAIChatSessionEventReader _reader;

    public LongSessionsReport(IAIChatSessionEventReader reader)
    {
        _reader = reader;
    }

    public static string Name => "my-long-ai-sessions";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LongSessionsReport>("Long AI chat sessions");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<LongSessionsReport>("Sessions with more than 20 messages.");

    public static string Category => ReportsConstants.Categories.AIReports;

    public static Permission Permission => ChatAnalyticsPermissionProvider.ViewChatAnalytics;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.AllTime;

    // Shows the shared AI profile filter (AIChatReportFilters.RequiredProfile makes it required).
    public IReadOnlyCollection<string> FilterNames { get; } = [AIChatReportFilters.Profile];

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var sessions = await _reader.ListAsync(new AIChatSessionEventQuery
        {
            ProfileId = context.Filter.GetOrDefault<string>(AIChatReportFilters.ProfileId),
            FromUtc = range.FromUtc,
            ToUtc = range.ToUtc,
        }, cancellationToken);

        return new ReportDocument().Add(ReportSection.ForMetrics("Long sessions",
        [
            new ReportMetric("Sessions", ReportFormat.Number(sessions.Count(session => session.MessageCount > 20))),
        ]));
    }
}
```

Register it with `services.AddReport<LongSessionsReport>()` in a startup marked `[RequireFeatures(ReportsConstants.Feature)]`. `IAIChatSessionEventReader` and `AIChatSessionEventQuery` (`ProfileId`, `FromUtc`, `ToUtc`, both bounds nullable) are in `CrestApps.OrchardCore.AI.Chat.Services`; `AIChatReportFilters` (`Profile`, `RequiredProfile`, `ProfileId`) is in `CrestApps.OrchardCore.AI.Chat.Reports`.

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
- The analytics pages became reports of the Reports framework: the old `AI/ChatAnalytics/Index`-style routes, `AIChatAnalyticsReport` and `AIChatAnalyticsFilter` are gone. Move a `DisplayDriver<AIChatAnalyticsReport>` section into its own `IReport` (above); a filter driver becomes a `DisplayDriver<ReportFilter>` gated by `IReportFilterMetadata`.
- Exports keep the old flat CSV columns; the reports open on **All Time** as the old pages did.
