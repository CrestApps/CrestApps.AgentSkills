# Contact Center Analytics Catalog

## Activation and Access

- The Contact Center reports activate when **Contact Center Work Distribution** (`CrestApps.OrchardCore.ContactCenter.Queues`) and **Reports** (`CrestApps.OrchardCore.Reports`) are both enabled. There is no separate feature to toggle.
- **Dialer activity and health** also needs **Paced Dialing**. Excel export needs `CrestApps.OrchardCore.Reports.OpenXml`; CSV is built in.
- Access requires `ViewContactCenterReports` (Supervisor and Administrator by default). CRM reports from Omnichannel Management require **View Omnichannel reports**.
- They appear under the admin **Reports** menu, grouped into Executive, Operations, Queue & Routing, Agent Performance, Workforce & Payroll, Billing & Usage, CRM & Campaigns, Compliance & Audit, and Technical & IT.
- They never depend on the capabilities they measure. One whose subject needs Voice or Recording renders a notice naming the missing feature; others drop the columns an absent capability would feed rather than showing zeroes.
- With `OrchardCore.DataPipelines.Core` enabled, `ContactCenterReportDataSource` exposes interaction, event, call session, call quality, queue, queue item, dialer profile, callback, recording, agent and shared voicemail data sets to the Report Builder.

For the framework itself (registering a report, filters, export formats) see `orchardcore-reports`.

## Shared Rules

- Population is the cohort whose `CreatedUtc` falls in the tenant-local range converted to UTC. Backlog and aging include all open work created by **To**.
- Every interaction has exactly one outcome from `InteractionOutcomeClassifier`: callback requested, abandoned, voicemail, answered, in progress, failed, or not connected (outbound).
- Totals and percentages are recomputed from raw counts at every level, never averaged across rows.
- Queue groups and campaign groups use current-membership semantics; moving a queue changes historical group attribution.
- User cells render the `UserDisplayName` shape; deleted users show **Unknown user**.

## Canonical KPIs

| KPI | Definition |
|---|---|
| Answer rate | Answered / all interactions; inbound answer rate is inbound answered / inbound offered |
| Abandonment rate | Abandoned / inbound offered; voicemail and callback-requested are offered but not abandoned |
| ASA | Sum of `AnsweredUtc - CreatedUtc` for inbound answered / inbound answered |
| AHT | Sum of connected duration plus completed wrap-up / handled interactions |
| Service level | Answered within the queue SLA / (answered + abandoned), per queue |
| Transfer rate | Answered interactions with a transfer / answered interactions |
| Recording coverage | Answered voice interactions with a recording reference / answered voice interactions |
| Agent utilization | (Busy + WrapUp) / observed signed-in time |
| Agent occupancy | (Busy + WrapUp) / (Available + Reserved + Busy + WrapUp) |

## Catalog Summary

| Area | Entries |
|---|---|
| Executive and operations | Executive performance dashboard (Chart.js), Call insights, Interaction volume trend, Interval performance, Hour-of-day, Day-of-week |
| Queue and routing | Queue usage, Queue service level, Queue abandonment analysis, Queue performance, wait and handle time, Queue transfer performance, Channel, Direction, Provider performance, Interaction outcome summary, Agent availability and wait time (live), Dialer decision log |
| Agents | Agent productivity, handle time, wrap-up performance, interaction volume, outcome, inbound, outbound, transfer and recording coverage, Talk/hold/ring and queue wait |
| Interactions | Interaction detail, Transfer analysis, Recording coverage, Interaction lifecycle duration, Long, Failed, Abandoned and High-wait interaction detail, Call leg performance, Call quality |
| Campaigns and CRM | Campaign summary (pending split into dialable now, held back, scheduled, not in any queue, assigned to a user, not explained), Subject inventory, Dialer queue inventory (live), Dialer call detail, activity summary, backlog, aging and source breakdowns |
| Workforce and payroll | Agent time summary, Daily agent timecard, Presence status duration, Break and away, Ready versus not-ready, Utilization, Occupancy, Presence reason breakdown, Agent presence audit, Queue and campaign signed-in hours, Payroll timecard inputs, Reconciled payroll timecard, Agent activity timeline |
| Billing and usage | Queue, agent, provider, channel and daily usage for billing (durations and counts, never money) |
| AI escalations | Containment and escalation, Escalation reasons, Post-escalation outcome |
| Dialer health | Dialer activity and health (`contact-center-dialer-activity`, Paced Dialing) |

Names used in URLs and filters include `contact-center-campaign-summary`, `contact-center-dialer-queue-inventory`, `contact-center-agent-availability` and `contact-center-dialer-activity`.

## Notable Entries

- **Agent availability and wait time** is live (ignores the period). It explains, per agent and per queue or campaign, why an available agent is or is not getting work, using the same `AgentRoutingEligibility` rules and `IActivityRoutingService.SelectAgentAsync` verdict routing uses. Stale-state flags need the Supervision feature (`IStaleAgentWorkService`).
- **Dialer decision log** reads `DialerDecisionIndex` and `DialerDecisionAgentIndex` through `IDialerDecisionLog` (registered by the base feature). It flags queues with due work and no routing decision for longer than `DecisionLogQuietThreshold`. A backfill background task indexes older events once.
- **Dialer queue inventory** and the Campaign summary split share `IDialerQueueInventoryService` and `DialerRecordClassifier` with the Live dashboard's dispatch card, so they always agree. At most 2,000 queued records per campaign are explained.
- **Dialer activity and health** reads `DialerPacingCycle` records and the per-profile `DialerAttemptStarted`, `DialerLiveAnswered` and `DialerCallAbandoned` events.

## Workforce Limits

Workforce entries read the agent state log. Periods above `MaximumWorkforceReportRange` (31 days) or exports above `MaximumWorkforceExportRange` are refused before any read, with a message. Reads are keyset-paged (1,000 events) on `IDX_InteractionEventIndex_Window`. The **Contact Center Agent Time Summaries** background task builds `AgentDailyTimeSummary` documents per UTC day (back to `AgentTimeSummaryBackfill`, 92 days); set it to `00:00:00` to turn summaries off. Everything else refuses ranges above `MaximumReportRange` (400 days).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContactCenter": {
        "Reporting": {
          "MaximumReportRange": "180.00:00:00",
          "MaximumWorkforceReportRange": "31.00:00:00",
          "MaximumWorkforceExportRange": "62.00:00:00",
          "AgentTimeSummaryBackfill": "92.00:00:00",
          "DecisionLogQuietThreshold": "00:10:00"
        }
      }
    }
  }
}
```

## Known Data Limitations

There are no forecast, schedule adherence, quality scorecard, CSAT, NPS, first-contact resolution, sentiment or money figures, because the source data is not collected. Activity breakdowns use current status and cannot reconstruct past backlog. Daily metrics are a rebuildable projection; after a restore run `IContactCenterMetricsProjectionMaintenanceService.RebuildAsync` (or `DetectDriftAsync` to check without changes).
