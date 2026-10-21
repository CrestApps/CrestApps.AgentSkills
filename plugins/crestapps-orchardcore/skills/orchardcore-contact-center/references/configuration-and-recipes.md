# Contact Center Configuration, Recipes and Deployment

## Configuration Sections

All sections live under `CrestApps:ContactCenter`. In the host `appsettings.json` wrap them in `OrchardCore`; in a tenant's `App_Data/Sites/{Tenant}/appsettings.json` omit the wrapper. Environment variables use `OrchardCore__CrestApps__ContactCenter__{Section}__{Key}`. Values bind to `IOptions<T>` when the tenant starts and are validated on activation, so an invalid value stops that tenant (not the host) and a change needs a tenant restart.

| Section | Options class | Feature | Controls |
|---|---|---|---|
| `Topology` | `ContactCenterTopologyOptions` | Contact Center | `ProfileId` (for example `single-node-distributed`); unknown ids fail |
| `Coordination` | `ContactCenterCoordinationOptions` | Contact Center | Inbound, assignment and reservation lock waits and leases, agent pre-dial, `DialerAgentParkingEnabled`, `DialerAgentParkWindow` |
| `FeatureLifecycle` | `ContactCenterFeatureLifecycleOptions` | Contact Center | `DrainTimeoutSeconds` (30, 1 to 300), `ShutdownDrainTimeoutSeconds` (5, 0 to 60) |
| `Retention` | `ContactCenterRetentionOptions` | Contact Center | `...RetentionDays` windows (0 keeps forever, `DialerPacingCycleRetentionDays` defaults to 14), floors, `PurgeBatchSize`, `MaxPurgeBatchesPerCycle` |
| `HealthChecks` | `ContactCenterHealthCheckOptions` | Contact Center + `OrchardCore.HealthChecks` | Dead-letter and overdue thresholds, `EnableNodeServingGate`, `AllowUnsafeSharedEndpointRoute` |
| `Availability` | `AgentAvailabilityOptions` | Agents | `HeartbeatTimeout` (00:01:30), `MaximumWrapUpDuration` (00:15:00), `OrphanedBusyGracePeriod` (00:01:00) |
| `Reporting` | `ContactCenterReportingOptions` | Queues + Reports | `MaximumReportRange` (400 days), `MaximumWorkforceReportRange` and `MaximumWorkforceExportRange` (31 days), `AgentTimeSummaryBackfill` (92 days), `DecisionLogQuietThreshold` (15 minutes) |
| `Compliance` | `ContactCenterComplianceOptions` | Dialer | `AbandonmentRollingWindowMinutes` (30, 1 to 1440) |
| `Compliance:ManualDialing` | `ManualDialingComplianceOptions` | Dialer | `RespectDoNotCall` (true), `EnforceCallingWindow`, `CallingCalendarId`, `DefaultRegionCode` |
| `DialerPacing` | `ContactCenterDialerPacingOptions` | Paced Dialing | `Debounce` (300 ms), `LockExpiration` (30 s), `LockRetryDelay` (1 s), `StalledAfter` (5 min) |
| `PredictiveDialing` | `ContactCenterPredictiveDialingOptions` | Paced Dialing | Over-dial timings (`PacingInterval`, `MaxDialsPerCycle`, `AgentLegAnswerTimeout`, `ComplianceWindowDays`, `DiscountAgentsWithWaitingInbound`, and more) |
| `WebhookIngress` | `ProviderWebhookIngressOptions` | Voice | Per-node `ConcurrencyPermitLimit` (8), `RatePermitLimit` (120), `RatePeriodSeconds` (60), `MaximumDeliveryAgeSeconds` (900), `MaximumFutureSkewSeconds` (120) |
| `BaseVoiceVerification` | `BaseVoiceVerificationOptions` | Voice | `AudioVerificationAcknowledged`, `AudioVerificationEvidenceReference` (required when acknowledged) |

Related sections owned by other modules: `CrestApps_Telephony:Commands:Timeout` (PBX mutation deadline, default 10 s) and `CrestApps_Telephony:Coordination` (see `orchardcore-telephony`).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Topology": {
          "ProfileId": "single-node-distributed"
        },
        "Availability": {
          "HeartbeatTimeout": "00:01:30",
          "MaximumWrapUpDuration": "00:10:00"
        },
        "Compliance": {
          "AbandonmentRollingWindowMinutes": 30,
          "ManualDialing": {
            "RespectDoNotCall": true,
            "DefaultRegionCode": "US"
          }
        },
        "Retention": {
          "InteractionEventRetentionDays": 400,
          "LegalHoldMinimumDays": 0,
          "ProjectionReplayHorizonDays": 90
        }
      }
    }
  }
}
```

## Site Settings (Settings → Contact Center)

Settings use the `contactcenter` group (`ContactCenterConstants.Settings.GroupId`). A tab appears only when its feature is enabled. They travel through the standard `Settings` recipe step, which replaces the whole object.

| Settings object | Feature | Key members |
|---|---|---|
| `ContactCenterExternalTransferSettings` | Voice | `Destinations` (`Id`, `DisplayName`, `E164Address`, `Enabled`), `AllowUnlistedNumbers` |
| `ContactCenterRecordingSettings` | Call Recording | `RecordAllCalls`, `ConsentModel`, `RequireExplicitConsent`, `EnableRecordingDisclosure`, `RecordingDisclosureText`, `DiscloseOnInboundCalls`, `DiscloseOnAIVoiceCalls`, `PromptAgentsToDisclose`, `RetentionDays`, `LegalHoldByDefault`, `AllowAgentSecurePause`, `MaxSecurePauseSeconds`, `RequirePauseReason` |
| `SecureCaptureSettings` | Secure Capture | `Enabled`, `LinkTimeToLiveSeconds` (30 to 3600, default 300), `PauseRecordingDuringCapture` |
| `ContactCenterDefaultAddressSettings` | Contact Center + Channel Endpoints | Default phone and SMS numbers for agents with no line |

```json
{
  "steps": [
    {
      "name": "Settings",
      "ContactCenterExternalTransferSettings": {
        "Destinations": [
          {
            "Id": "after-hours-desk",
            "DisplayName": "After-hours desk",
            "E164Address": "+15551230000",
            "Enabled": true
          }
        ],
        "AllowUnlistedNumbers": false
      },
      "ContactCenterRecordingSettings": {
        "RecordAllCalls": true,
        "ConsentModel": "AllParties",
        "EnableRecordingDisclosure": true,
        "RecordingDisclosureText": "This call may be recorded for quality assurance and training purposes.",
        "RetentionDays": 365,
        "AllowAgentSecurePause": true,
        "MaxSecurePauseSeconds": 300
      }
    }
  ]
}
```

## Recipe and Deployment Steps

Each configurable entity has a deployment step (category **Contact Center**) and a recipe step. A step is offered only when its owning feature is enabled; deployment steps need `OrchardCore.Deployment` and recipe steps need `OrchardCore.Recipes.Core`. JSON schemas for these steps ship with the CrestApps Recipes feature (see `orchardcore-crestapps-recipes`). Enums are written as their names.

| Recipe step | Collection | Owning feature | Identified by |
|---|---|---|---|
| `ContactCenterSkill` | `Skills` | Queues | `ItemId` |
| `ContactCenterQueueGroup` | `QueueGroups` | Queues | `ItemId` |
| `ContactCenterBusinessHoursCalendar` | `Calendars` | Business Hours | `ItemId` |
| `ContactCenterQueue` | `Queues` | Queues | `ItemId` |
| `ContactCenterEntryPoint` | `EntryPoints` | Entry Points | `ItemId` |
| `ContactCenterDialerProfile` | `DialerProfiles` | Dialer | `ItemId` |
| `AgentStateReasonCode` | `ReasonCodes` | Agents | `ItemId` |
| `ContactCenterAgentEntitlement` | `Agents` | Agent Entitlements | `UserName` |
| `ContactCenterVoiceMedia` | `VoiceMedia` | Contact Center | `ItemId` |

Import rules:

- An existing `ItemId` is updated; an unknown `ItemId` is created under that id; an omitted `ItemId` gets a new id. This makes plans idempotent.
- Entries are validated by the entity's own handlers, the same as the admin editor. A rejected entry is reported and skipped; the rest still import. Out-of-range values are refused, not clamped.
- References are checked, so order steps: voice media, skills, queue groups, calendars → queues → entry points → dialer profiles → agent entitlements. Put Omnichannel steps (campaigns, channel endpoints) before the Contact Center steps that reference them.
- Agent entitlements match users by `UserName`, skip unknown users, drop dangling queue and campaign ids, and never touch live presence.
- Runtime state (activities, interactions, events, call sessions, queue items, reservations, sessions, callbacks, commands, inbox and outbox) never travels. Voice media clips travel as library entries without audio.

### Example plan

```json
{
  "steps": [
    {
      "name": "ContactCenterSkill",
      "Skills": [
        { "ItemId": "skill-billing", "Name": "Billing", "Description": "Billing questions.", "Enabled": true }
      ]
    },
    {
      "name": "ContactCenterBusinessHoursCalendar",
      "Calendars": [
        {
          "ItemId": "cal-support",
          "Name": "Support hours",
          "TimeZoneId": "America/New_York",
          "WeeklySchedule": [
            { "Day": "Monday", "IsOpen": true, "OpenMinute": 480, "CloseMinute": 1020 },
            { "Day": "Tuesday", "IsOpen": true, "OpenMinute": 480, "CloseMinute": 1020 }
          ],
          "Holidays": [ "2026-12-25" ],
          "Enabled": true
        }
      ]
    },
    {
      "name": "ContactCenterQueue",
      "Queues": [
        {
          "ItemId": "queue-billing",
          "Name": "Billing Voice",
          "DefaultPriority": "Normal",
          "RoutingStrategy": "LongestIdle",
          "PreferStickyAgent": true,
          "SlaThresholdSeconds": 60,
          "ReservationTimeoutSeconds": 30,
          "UnansweredOfferAction": "Requeue",
          "BusinessHoursCalendarId": "cal-support",
          "AfterHoursAction": "HoldInQueue",
          "SkillRequirements": [
            { "SkillId": "skill-billing", "MinimumProficiency": 3, "Required": true, "RelaxAfterSeconds": null }
          ],
          "MaxWaitSeconds": 900,
          "MaxWaitAction": "Voicemail",
          "Treatment": {
            "WelcomeMessage": "Thanks for calling billing.",
            "AnnouncementIntervalSeconds": 60,
            "AnnouncePosition": true,
            "CallbackDtmfKey": "1",
            "CallbackOfferAfterSeconds": 120
          },
          "Enabled": true
        }
      ]
    },
    {
      "name": "ContactCenterEntryPoint",
      "EntryPoints": [
        {
          "ItemId": "ep-main",
          "Name": "Main line",
          "Channel": "Phone",
          "AddressIds": [ "OMNICHANNEL-ADDRESS-ID" ],
          "TargetType": "Queue",
          "TargetQueueId": "queue-billing",
          "Priority": "Normal",
          "BusinessHoursCalendarId": "cal-support",
          "ClosedAction": "Voicemail",
          "VoicemailDestination": "QueueSharedBox",
          "WelcomeMessage": "Welcome to Example Co.",
          "ClosedMessage": "We are closed. Please leave a message.",
          "IvrFlow": {
            "RootNodeId": "main",
            "MaxRetries": 3,
            "FallbackAction": { "Kind": "RouteToQueue", "TargetId": "queue-billing" },
            "Nodes": [
              {
                "NodeId": "main",
                "Prompt": "Press 1 for billing or 9 to leave a message.",
                "PromptMediaId": null,
                "Options": [
                  { "Digit": "1", "Action": { "Kind": "RouteToQueue", "TargetId": "queue-billing" } },
                  { "Digit": "9", "Action": { "Kind": "Voicemail", "TargetId": null } }
                ]
              }
            ]
          },
          "Enabled": true
        }
      ]
    },
    {
      "name": "ContactCenterDialerProfile",
      "DialerProfiles": [
        {
          "ItemId": "dp-renewals",
          "Name": "Renewals power",
          "Mode": "Power",
          "CallsPerAgent": 2,
          "MaxAttempts": 3,
          "RetryDelayMinutes": 240,
          "RingTimeoutSeconds": 30,
          "DefaultRegionCode": "US",
          "RespectDoNotCall": true,
          "EnforceCallingWindow": true,
          "CallingCalendarId": "cal-support",
          "EnforceAbandonmentCap": true,
          "MaxAbandonmentRatePercent": 3,
          "SafeHarborEnabled": true,
          "SafeHarborMessage": "This is {company} calling. Please call us back at {number}.",
          "Enabled": true
        }
      ]
    },
    {
      "name": "AgentStateReasonCode",
      "ReasonCodes": [
        { "Name": "Lunch", "AppliesTo": "Break", "SortOrder": 1, "Enabled": true }
      ]
    },
    {
      "name": "ContactCenterAgentEntitlement",
      "Agents": [
        {
          "UserName": "agent.smith",
          "DisplayName": "Agent Smith",
          "MaxConcurrentInteractions": 1,
          "AllowedQueueIds": [ "queue-billing" ],
          "AllowedCampaignIds": [],
          "Skills": [ "Billing" ]
        }
      ]
    }
  ]
}
```

`OpenMinute` and `CloseMinute` are minutes after midnight; equal values mean a 24-hour day and windows may cross midnight. IVR action kinds are `RouteToQueue`, `RouteToAgent`, `SubMenu`, `Voicemail`, `ExternalTransfer` (target is an external destination id) and `Repeat`. A `null` `FallbackAction` routes to the entry point's own target. Predictive profiles add `PredictivePacingModel` (`ReservedPerCall` or `OverDial`) and the over-dial properties described in [routing-and-dialer.md](routing-and-dialer.md).

## Feature-Only Recipes

`contact-center-asterisk-ga-core.recipe.json` (recipe name `ContactCenterAsteriskGaCore`) enables the certified `ga-core-asterisk` set (Contact Center, Agents, Queues, Inbound Voice, Telephony Soft Phone, Dialer, Asterisk). Run it from **Configuration → Recipes** with `OrchardCore.Recipes` enabled. The Agents feature also runs the `agent-state-reason-codes` module recipe at setup, seeding standard reason codes with fixed identifiers.

## Making a New Entity Portable

Add the four standard pieces (deployment source, deployment step, step display driver, recipe step handler) plus a catalog handler that enforces the entity's rules. The recipe step writes through the entity's manager, so the same rules run for recipes, deployment plans and the editor. Register the handler in the same feature as the recipe step. The shipped module splits registration by required feature:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;

[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Queues", "OrchardCore.Deployment")]
public sealed class MyEntityDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<MyEntityDeploymentSource, MyEntityDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, MyEntityDeploymentStepDisplayDriver>();
    }
}

[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Queues", "OrchardCore.Recipes.Core")]
public sealed class MyEntityRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<MyEntityStep>();
    }
}
```

`MyEntityStep` derives from `NamedRecipeStepHandler`, as the shipped `ContactCenterQueueStep` does. See `orchardcore-deployments` and `orchardcore-recipes` for the step contracts.
