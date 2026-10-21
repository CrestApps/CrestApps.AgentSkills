# Contact Center Workflows

The Workflows bridge is not a feature. Enable `OrchardCore.Workflows` alongside Contact Center and the event activity appears; each task also requires the capability that owns its service, so it only shows in the designer when it can run. Every activity is in the **Contact Center** category. The Subject Flow remains the primary completion logic; use workflows to enrich or react, never to bypass queues, reservations or `IActivityDispositionService`.

## Contact Center Event

`ContactCenterEvent` starts or resumes a workflow when a domain event is published. **Event type** is a grouped picker of every canonical event, or **Any event type**. Outcomes are **Matched** and **Ignored**.

The workflow input carries `EventType`, `InteractionId`, `AggregateType`, `AggregateId`, `ActorId`, `ActorType` (agent, supervisor, workflow, provider or `system`), `AgentId`, `AgentUserId`, `SourceComponent` and `Data`. Read the agent the change is about from `{{ Workflow.Input.AgentUserId }}`, not `ActorId`.

| Event | `Data` payload | Notable properties |
|---|---|---|
| `DialerAttemptCompleted` | `CallLifecycleEventData` | `Outcome` (`Answered`, `NoAnswer`, `Busy`, `AnsweringMachine`, `NotInService`, `Rejected`, `Failed`), `PhoneNumber`, `ActivityItemId`, `CampaignId`, `HangupCause` |
| `DialerLiveAnswered` | `CallLifecycleEventData` | `InteractionId`, `AgentId`, `QueueId`, `Details.dialerProfileId` |
| `DialerCallAbandoned` | `CallLifecycleEventData` | `Reason` (`agent_leg_failed`, `agent_connect_failed`, `agent_unavailable`, `agent_connected_late`, `customer_hung_up_waiting`), `DurationSeconds` |
| `ActivityDispositionApplied` | `ActivityDispositionEventData` | `ActivityItemId`, `DispositionName`, `Outcome`, `Source` (`Agent`, `AI`, `Provider`, `Workflow`, `System`), `CampaignId`, `PhoneNumber`, `Attempts` |
| `RoutingDecisionMade` | `ActivityRoutingDecisionEventData` | Identical failed repeats are coalesced; read `OccurrenceCount` and `LastSeenUtc` |

Other event families include interactions, offers (`OfferAccepted`), agents (`AgentPresenceChanged`, `AgentSignedIn`), calls (`CallConnected`, `CallEnded`), callbacks (`CallbackRequested`), recording (`RecordingDisclosed`, `RecordingDenied`, `RecordingAutoResumed`), supervision, secure capture and shared voicemail. Event names are constants in `ContactCenterConstants.Events`.

## Tasks

| Task | Class | Owning feature | Outcomes |
|---|---|---|---|
| **Set Agent Presence** (User, Status, Reason) | `SetAgentPresenceTask` | Agents | Done, Failed. `Reserved`, `Busy` and `WrapUp` are rejected |
| **Enqueue Activity** (Activity, Queue, Priority) | `EnqueueActivityTask` | Queues | Done, Failed (missing queue or activity) |
| **Hand Off to Live Agent** (Activity, Queue, Reason, Summary) | `TransferToAgentTask` | Queues | Connected, Waiting In Queue, Callback Scheduled, Failed |
| **Schedule Callback** (Destination, Delay, Campaign, Queue, Contact) | `ScheduleCallbackTask` | Dialer | Done, Failed |
| **Schedule Dialer Retry** (Activity, Delay minutes) | `ScheduleDialerRetryTask` | Dialer | Scheduled, Exhausted, Failed |
| **Start Call Recording** / **Stop Call Recording** (Interaction) | `StartCallRecordingTask`, `StopCallRecordingTask` | Call Recording | Done, Indeterminate, Failed |
| **Place Call or Send Message** (Activity) | `StartOmnichannelActivityTask` | Contact Center | Done, Already Started, Failed |
| **Find Agent Numbers** (User name) | `FindAgentNumbersTask` | Contact Center + `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints` | Done, NotFound. Writes `Workflow.Output.AgentPhoneNumber` and `AgentSmsNumber` |

Text inputs accept Liquid, for example `{{ Workflow.Input.InteractionId }}` or `{{ Workflow.Input.Data.ActivityItemId }}`.

- **Indeterminate** means the provider may have changed recording state but the outcome could not be observed; branch to reconciliation or alerting.
- **Already Started** prevents a duplicate call when a workflow fires twice or races the periodic automated-activities pass; only `NotStated` or `Scheduled` activities start.
- **Hand Off to Live Agent** escalates an automated (AI) conversation through `IOmnichannelHandoffService`; without a queue it uses the subject flow's handoff queue. It never touches a call a human is on.
- **Schedule Dialer Retry** queues the next attempt in the same campaign and profile, honoring max attempts; a shorter delay than the profile's retry delay is raised to it.
- **Send Text Message** belongs to the SMS messaging feature (`CrestApps.OrchardCore.Omnichannel.Messaging.Sms`), not Contact Center; see the Omnichannel messaging skills.

## Deliberately Excluded

- **Transfer a live call**: transfers are authorized against the initiating agent's `ClaimsPrincipal` (`IContactCenterTransferService`); a background workflow has none.
- **Assign work to a specific agent**: assignment belongs to routing. Use **Enqueue Activity** and let routing honor presence, skills, entitlements and reservations.

## Example: record connected campaign calls

```json
{
  "steps": [
    {
      "name": "WorkflowType",
      "data": [
        {
          "WorkflowTypeId": "cc-record-connected",
          "Name": "Record connected calls",
          "IsEnabled": true,
          "IsSingleton": false,
          "DeleteFinishedWorkflows": true,
          "Activities": [
            {
              "ActivityId": "evt",
              "Name": "ContactCenterEvent",
              "X": 0,
              "Y": 0,
              "IsStart": true,
              "Properties": { "EventType": "CallConnected" }
            },
            {
              "ActivityId": "rec",
              "Name": "StartCallRecordingTask",
              "X": 300,
              "Y": 0,
              "IsStart": false,
              "Properties": { "InteractionId": "{{ Workflow.Input.InteractionId }}" }
            }
          ],
          "Transitions": [
            { "SourceActivityId": "evt", "SourceOutcomeName": "Matched", "DestinationActivityId": "rec" }
          ]
        }
      ]
    }
  ]
}
```

Building workflows in the designer is easier than hand-writing activity properties; export from the designer to get exact property names. JSON schemas for the Contact Center workflow activities ship with the CrestApps Recipes feature (see `orchardcore-crestapps-recipes`).

## Code Alternative

For logic that must not depend on Workflows, implement `IContactCenterEventHandler` (see the SKILL.md sample). Declare a stable `HandlerId` and an honest `ContactCenterHandlerReplaySafety`:

| Value | Meaning |
|---|---|
| `NaturallyIdempotent` | Repeating the effect converges (logging, idempotent upserts) |
| `DeduplicatedByEventId` | Reserve a durable `(HandlerId, EventId)` marker in the same transaction as the effect |
| `GuardedByDurableStore` | A downstream unique constraint or compare-and-set makes replay safe |
| `Unspecified` | Rejected at registration |

The Workflows bridge itself uses event-id deduplication, so a redelivered event never starts a duplicate workflow.
