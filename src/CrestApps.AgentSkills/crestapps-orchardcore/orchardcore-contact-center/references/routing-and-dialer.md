# Agents, Routing and the Dialer

## Agent Presence and Availability

| State | Set by | Routing |
|---|---|---|
| `Offline` | Agent or system | Ineligible; signing out clears queue and campaign membership |
| `Available` | Agent or system | Eligible; entering it triggers queued voice recovery and dialer pacing |
| `Reserved` | System | An offer is pending, or a paced dial holds the agent (`AgentProfile.DialHoldReservationId`) |
| `Busy` | System | On an accepted interaction |
| `WrapUp` | System | Answered queue or campaign call ended; released by completing the activity or after `MaximumWrapUpDuration` |
| `RequestBreak` | Agent | Becomes `Break` when current work ends |
| `Break`, `Away`, `DoNotDisturb`, `Meeting`, `Training`, `AfterHoursUnavailable` | Agent (reason codes) | Not ready |

- Routing joins an `Available` profile with entitlement, the session's selected queue, an online connection, a fresh heartbeat (client sends every 30 seconds; `HeartbeatTimeout` default 90 seconds) and spare `MaxConcurrentInteractions`. The rules live in `AgentRoutingEligibility`, used by `AgentAvailabilityService` and the availability report.
- Every transition goes through `IAgentStateTransitionService` and is stored as a durable event (`AgentPresenceChanged`, `AgentStateChanged`, `AgentSignedIn`) with previous, current and requested state, reason and memberships. Workforce reports derive from it.
- `IdleSinceUtc` drives longest-idle routing. Work that never reached the agent (caller hung up while ringing, compensated dial, unanswered campaign call) sets `AgentStateChangeContext.KeepsIdlePlace` so the agent keeps their place.
- An agent whose accepted leg is refused (SIP 480) is released through `IActivityReservationService.ReleaseUnreachableAgentAsync`; the queue's unanswered-offer action applies to the caller and the agent goes `Away` with *Phone unreachable* (`AgentReleaseReasons.Unreachable`).
- A ringing offer whose pre-dialed leg is refused as unavailable (SIP 480 or 404) with no other credential to ring is reported through `IAgentPreDialCoordinator.OnAgentPhoneUnreachableAsync` and released at once by `IActivityReservationService.ReleaseUnreachableOfferAsync`, as an offer that rang out, with the agent set `Away` (*Phone unreachable*). A busy or declining phone (486, 603), a direct line and a campaign offer still ring out.
- A disposition recorded on an earlier activity while the agent is on another call, or wrapping up a later one, ends only that activity's wrap-up; the agent's presence is left alone and no queued work is offered to them.
- The agent bar's state carries `phoneOpen` from `IAgentPhoneStatusProvider` (Telnyx answers from its credentials; `null` when no provider can tell), and warns a signed-in agent with no open phone.
- Background tasks recover orphaned state each minute: session cleanup (`AgentSessionCleanupBackgroundTask`), wrap-up deadline and orphaned `Busy` (`OrphanedBusyGracePeriod`) recovery (`AgentAvailabilityRecoveryBackgroundTask`).
- Reason codes (`AgentStateReasonCode`: `Name`, `AppliesTo`, `SortOrder`, `Enabled`) replace the built-in not-ready list in the presence menu. A code that applies to `Break` submits a break request.
- With **Agent Entitlements** disabled, entitlements are permissive and sign-in grants the chosen queues onto the profile. With it enabled, only granted queues and campaigns are offered, and revoking one prunes live membership immediately.

## Queues

Key `ActivityQueue` members: `DefaultPriority` (`Lowest` to `Highest`), `RoutingStrategy`, `PreferStickyAgent`, `EnableSlaAging`, `SlaThresholdSeconds` (120), `ReservationTimeoutSeconds` (30), `UnansweredOfferAction`, `BusinessHoursCalendarId`, `AfterHoursAction` (`HoldInQueue`, `Overflow`), `OverflowQueueId`, `OverflowAfterSeconds`, `OverflowTargets`, `RequiredSkills`, `SkillRequirements`, `MaxWaitSeconds`, `MaxWaitAction`, `MaxQueueSize`, `QueueFullAction`, `Treatment`, `InboundChannelEndpointId`, `FirstResponseTargetSeconds` (messaging), `QueueGroupId`.

- Skills are held at proficiency 1 to 5. `RequiredSkills` means proficiency 3 or more; `SkillRequirements` sets a per-skill minimum, marks a skill preferred instead of required, and can relax after a wait. Skill names compare trimmed and case-insensitively; interior spaces matter.
- Queue groups are catalog and reporting metadata only, with current-membership semantics in reports.
- Waiting work stays durable when no agent is available. Assignment runs on sign-in, on return to `Available`, on events, and every minute as the backstop.
- Overflow preserves the original enqueue time for SLA aging and cannot form a cycle.
- Offer deadlines, overflow hops, max wait and treatment steps are held in process by `IContactCenterDeadlineScheduler` and re-armed at tenant start; minute tasks (`ReservationExpiryBackgroundTask`, `QueueTreatmentBackgroundTask`) are the durable backstop.
- After a decline, the item is re-offered first to agents who have not declined it, then earlier decliners, then the transferring agent, then the latest decliner. Nobody is held back when they are the only free agent.
- When an offer that rang out, or an accepted offer whose agent could not be reached, puts a caller who is still on the line back into a regular queue, the release publishes `OfferRequeued` after `AgentReleased`, and `ReofferVoiceWorkHandler` offers the next agent in line at once instead of at the next minute's pass. This also covers a Voicemail or Reject queue that fell back to requeueing. Direct lines and campaign virtual queues are not re-offered this way.

## Routing Pipeline

`IActivityRoutingService.SelectAgentAsync` runs every registered `IActivityRoutingStrategy` in `Order` against `ActivityRoutingContext` (`Queue`, `QueueItem`, `Candidates`). Shipped strategies: `RequiredSkillsRoutingStrategy`, `PreferredSkillsRoutingStrategy`, `CapacityRoutingStrategy`, `StickyAgentRoutingStrategy` (Order 30, boost 1000), `LongestIdleRoutingStrategy`, `RoundRobinRoutingStrategy`, `LeastBusyRoutingStrategy`. Only the queue's selected scoring strategy scores. Each candidate carries `IsEligible`, `Score` and `Reasons`, which appear in the `RoutingDecisionMade` event and the dialer decision log.

A custom strategy that prefers agents who speak the caller's language:

```csharp
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

public sealed class SpanishLineRoutingStrategy : IActivityRoutingStrategy
{
    // Run after eligibility filters (required skills, capacity) and before scoring tie-breaks.
    public int Order => 35;

    public ValueTask ApplyAsync(ActivityRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(context.Queue.Name, "Spanish line", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var candidate in context.Candidates)
        {
            if (candidate.IsEligible && candidate.Agent.Skills.Contains("Native Spanish", StringComparer.OrdinalIgnoreCase))
            {
                candidate.Score += 500d;
                candidate.AddReason("Native Spanish speaker.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
```

Register with `services.AddScoped<IActivityRoutingStrategy, SpanishLineRoutingStrategy>();` in a startup marked `[RequireFeatures("CrestApps.OrchardCore.ContactCenter.Queues")]`. Never set `IsEligible = true` on a candidate another strategy excluded, and never read or write the database per candidate.

## Reservations and Locks

- Assignment holds a per-queue lock; reservation creation takes the activity lock then the agent lock; accept, reject, cancel and expiry share a per-reservation lock. Leases are 30 seconds and never renewed.
- Correctness comes from YesSql document-version compare-and-set plus portable unique claim keys (one active queue item per activity, one pending or accepted reservation per activity, one pending reservation per agent). A loser aborts its scope.
- Lock timings are in `CrestApps:ContactCenter:Coordination`. Multi-node needs `OrchardCore.Redis.Lock`; a Redis backplane alone does not make `IDistributedLock` cross-node.

## Work State

```csharp
await _workStateService.MutateAsync(activityItemId, workState =>
{
    workState.AssignmentStatus = ActivityAssignmentStatus.Reserved;
    workState.ReservationId = reservation.ItemId;
    workState.ReservedById = agent.UserId;
    workState.ReservationExpiresUtc = reservation.ExpiresUtc;
}, cancellationToken);

await _activityWriter.ScheduleUpdateAsync(activityItemId, activity =>
{
    activity.Status = ActivityStatus.Cancelled;
    activity.TerminalReasonCode = reasonCode;
}, cancellationToken);
```

- `IContactCenterWorkStateService` owns assignment, reservation and attempts (`ContactCenterWorkState`, unique on `ActivityItemId`). Read it for any routing decision.
- `IContactCenterActivityWriter` schedules CRM-owned writes after the routing transaction commits. Only `ContactCenterWorkStateProjector` writes the ten projected routing fields on `OmnichannelActivity`.
- When the CRM ends, purges or deletes an activity, `ContactCenterActivityRoutabilityHandler` asks `IQueuedWorkWithdrawalService` to remove waiting work, revoke ringing offers, and leave accepted work with the agent. Each withdrawal records `QueueItemWithdrawn`.
- Missing work state is adopted from the activity's projected fields on first access, so upgrades need no backfill.

## Dialer Profiles

| Property | Notes |
|---|---|
| `Mode` | `Preview` (base Dialer) or `Power`, `Progressive`, `Predictive` (Paced Dialing). Saving a paced mode without Paced Dialing is rejected; `Manual` re-saves as `Preview` |
| `CallsPerAgent` | Power and Predictive, max 3 (`PowerDialerStrategy.MaxCallsPerAgent`). A limit per look at the campaign, not per agent: each call still reserves its own available agent |
| `PreviewOfferTimeoutSeconds` | Preview only, **Offer time (seconds)**, default 180 (15 to 3600; 0 uses the default). How long a preview record stays reserved for its agent; a campaign queue is never stored, so it replaces the 30-second queue default |
| `MaxAttempts`, `RetryDelayMinutes` | Fallback limits; a subject flow **Try Again** with its own maximum takes precedence |
| `RingTimeoutSeconds` | Default 30, clamped 15 to 120 for automated modes |
| `AnsweringMachineDetection` | `Disabled`, `Standard`, `Premium` |
| `CallerId`, `AlwaysUseCallerId` | Caller ID fallback, or forced |
| `DefaultRegionCode` | Region for national-format destinations (E.164 canonicalization) |
| `RespectDoNotCall`, `EnforceCallingWindow`, `CallingCalendarId` | Compliance gates; per-region calendar overrides are supported |
| `EnforceAbandonmentCap`, `MaxAbandonmentRatePercent`, `AbandonmentSampleFloor`, `SafeHarborEnabled`, `SafeHarborMessage` | Abandonment cap and the abandoned-call message (`{company}`, `{number}`). The message is required when the cap is enforced |
| `ShowDialProgressToAgent` | Show *Dialing…*, *Screening…*, *Connecting…* while the agent is held (default off) |
| `PredictivePacingModel` | `ReservedPerCall` (default) or `OverDial` |
| `TargetAbandonmentRatePercent` (2), `MaxLinesPerAgent` (2, 1 to 5), `MaxCallsInFlight` (100), `AnswerRateSampleFloor` (50), `AnswerRateWindowMinutes` (15), `CreditAgentsFreeingUp`, `FreeUpCreditPercent` (50), `ConnectWaitMilliseconds` (0, max 1500), `AbandonedRetryRequiresAgent` (true) | Predictive settings, validated by `DialerProfileHandler` on every save, import and deployment |

A profile names no queue or campaign. **Load Activities** with the **Dialer** source picks the campaign and profile; activities load unassigned and are queued on the campaign's virtual queue (`__campaign-queue__{campaignId}`). A campaign's waiting records must share one profile. Turning a profile off holds its records in the queue.

- Dialer loads skip and count contacts with no number to dial (`TotalSkippedForNoDestination`), and **Change Dialer Profile** leaves such activities unchanged. When an activity has no destination, the eligibility check dials the contact's current preferred phone number instead and runs every check on it.
- An activity load's **Schedule at** is stored in UTC and shown in local time.
- Opening the Complete screen gives a pending preview offer its full offer time again, and the screen pings a keep-alive every minute (`IActivityOfferKeepAlive`, `IActivityReservationService.ExtendPendingAsync`, never shortening). An agent who lost the record is redirected with a reason instead of a 403. `ContactCenterActivityOwnershipAuthorizationHandler` grants **Complete activity** from the work state's `ReservedById`/`AssignedToId`, so a just-offered record opens before the activity projection catches up.

## Pacing

- `IDialerPacingScheduler` (tenant singleton) paces a campaign queue within about a second of `AgentStateChanged`/`AgentPresenceChanged` to `Available`, `AgentSignedIn`, `AgentReleased`, `DialerAttemptCompleted`, `CallEnded` or `QueueItemAdded`. Requests debounce (`Debounce`, 300 ms) and merge per queue; each run takes `ContactCenterDialerPacing:{queueId}` without waiting and runs `IDialerService.RunCycleAsync`.
- `DialerPacingBackgroundTask` runs each minute as the backstop (`RequestBackstop`) with a 90-second budget.
- Over-dialing uses `IPredictivePacingScheduler`, `PredictiveOverDialPacer` and the pure `PredictiveOverDialCalculator`, which falls back to `ReservedFallback` whenever measurements cannot prove safety. `PredictiveAgentConnector` claims the longest-idle free agent at answer; nobody free means the abandoned-call message (`no_agent_available`).
- Paced agents stay `Reserved` until a person answers (`AcceptForDialAsync`, then `IAgentPresenceManager.ConnectHeldAgentAsync`). With `DialerAgentParkingEnabled` the agent leg is rung and held while the customer rings, so the answer only joins the two. The join is recorded the moment the bridge succeeds, in its own commit, so the agent is never left *Waiting for a connected call* while talking.
- The call connects without an **Answer** click. `AgentDesktopSnapshot` and every `AgentPresenceNotification` carry `DialHoldReservationId` (the dial the agent is held for), the soft phone re-reads the snapshot on every connect, and it answers only legs tagged for its own user. The soft phone never renews its credential under a parked or live leg.
- A dial is dispatched only while its reservation is accepted, the agent is still held for it (`Reserved` with that dial hold for paced modes, `Busy` for preview and callback dials) and on no other voice call, so a restart cannot ring two customers for one agent.
- `ReserveAsync` cancels an orphaned pending or accepted reservation left on an activity whose queue item is waiting before it creates the new one, so one stuck activity cannot stall a campaign queue.
- When a dialed call connects, the docked agent bar opens and the activity's **Complete activity** page opens on its own (`activeInteraction.autoOpenActivity`, once per call per browser tab, never over unsaved changes), and again when the call ends.
- Every cycle writes a `DialerPacingCycle` record (`Placed`, `NothingDue`, `NoAgents`, `HeldBack`, `LockNotAcquired`, `TimeBudgetExceeded`, `Failed`, `WriteConflict`). `IDialerPacingHealthService` turns them into `NothingToCall`, `Starting`, `Dialing`, `Idle`, `HeldBack`, `Stalled` or `LockNotAcquired` (the most severe: misses longer than the larger of the cycle timeout and twice `LockExpiration`; the banner reads **Dialer not pacing**).

## Outbound Compliance Gate

`IDialerEligibilityService` runs before every attempt and records `DialSuppressed` with a `DialerSuppressionReason`:

1. Destination resolves to E.164 (`NoDestination` otherwise, terminal).
2. Maximum attempts not reached (terminal).
3. Retry cool-down elapsed (retryable).
4. Contact `DoNotCall` opt-out when `RespectDoNotCall` (terminal).
5. Calling window open in the contact's time zone; a missing calendar fails closed (retryable).
6. Abandonment cap for automated modes, fail closed below the sample floor (retryable).
7. National registries (`INationalDoNotCallRegistry`, see `orchardcore-dnc-registry`); an unreachable registry suppresses with `ComplianceScreeningUnavailable`, which is retryable.

Soft-phone keypad dials are screened by the Dialer's `IOutboundCallScreener` under `Compliance:ManualDialing` and record `ManualDialSuppressed`. Queued callbacks skip do-not-call and calling-window screening because the customer asked for the call.

Abandonment is measured per profile from `DialerLiveAnswered` and `DialerCallAbandoned` events (`DialerAbandonment.ConnectThreshold` is two seconds) by `IDialerAbandonmentTracker` and `InteractionEventDialerAbandonmentStatisticsProvider`. Use one profile per campaign when each campaign's rate must stand alone.

## Durable Provider Commands

Dial, answer, connect, voicemail and reject commands are staged with the interaction, event and outbox in one commit, then executed by a fenced, leased state machine (`Pending`, `Claimed`, `Sent`, `OutcomeUnknown`, `Confirmed`, `Compensating`, terminal). HTTP 408, 429, 5xx and timeouts become `OutcomeUnknown` and are reconciled, never blindly retried. `ProviderCommandRecoveryBackgroundTask` re-evaluates pending dials against current policy each minute.

## Callbacks

`CallbackRequest` records contact, destination, optional campaign and queue, due window, attempts, status and notes. `CallbackDispatchBackgroundTask` promotes due requests into outbound `Callback` activities and enqueues them when a queue is set, at the queue's default priority, where they are offered like a preview call. The attempt limit, cool-down and not-in-service check still apply, and the agent who takes the callback dispositions it. A queue's callback offer (`Treatment.CallbackDtmfKey`) schedules one while the caller keeps their place; it needs the Outbound Dialer. Workflows use **Schedule Callback** or **Schedule Dialer Retry**.

## Outbound Lines

Part of Contact Center Voice, no feature to enable. On each phone **Channel endpoint**, **Outbound line** lists the agents who dial from that number; an agent may be on one line only. Caller ID resolution:

| Call | Caller ID |
|---|---|
| Keypad or extension call | Agent's line, else provider default |
| Dialer attempt | Load's **Dial from** number, agent's line, profile `CallerId`, provider default (`AlwaysUseCallerId` forces the profile number) |
| Queued callback | Agent's line, profile `CallerId`, provider default |

The server always decides; a caller ID sent by the browser is ignored. Transfers, consults and supervisor legs use the provider default.
