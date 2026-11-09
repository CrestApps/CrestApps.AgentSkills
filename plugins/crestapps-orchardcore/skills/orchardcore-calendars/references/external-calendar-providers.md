# External Calendar Providers

The **External Calendars** feature (`CrestApps.OrchardCore.Calendars.External`) owns the whole synchronization loop; a **provider** only talks to the external API.

## What Ships Today

| Provider | Name | Direction | Ships |
|---|---|---|---|
| iCalendar address (`ICalendarUrlProvider`) | `ics` | Read only, full read each run | Yes, registered by the External Calendars feature |
| Google Calendar | (none) | | **No** |
| Microsoft 365 / Outlook (Microsoft Graph) | (none) | | **No** |

Only the contracts for incremental and two-way providers ship (`IIncrementalCalendarProvider`, `IWritableCalendarProvider` in `CrestApps.OrchardCore.Calendars.Core.External`). Google Calendar and Microsoft 365 providers are expected as separate modules (issue #1238). Until then, subscribe to the `.ics` address that Google Calendar or Outlook publishes, and use **Calendar Feeds** to publish an Orchard calendar to those apps.

## What the Feature Does for You

`CalendarSyncBackgroundTask` runs every five minutes, asks `CalendarSyncService.ListDueAsync` for due calendars, and syncs each in its own child scope. A calendar is due when its interval has passed since the last attempt (doubling after each failure, up to 32 times) or when it has local changes waiting (`PendingPush`). A subscription that `NeedsReconnect` is skipped until saved again.

One run:

1. **Read.** With an `IIncrementalCalendarProvider` and a stored `SyncToken`, `PullChangesAsync` returns only changes; otherwise `PullAsync` returns everything and events missing from it are removed. `FullSyncRequired` drops the token and reads everything; after a full read `GetSyncTokenAsync` gives the next token.
2. **Import.** `ICalendarImporter` matches each `ICalendarEventData` by `Uid` among this subscription's events (`Source = External`), and records a `CalendarProviderMapping` (calendar id, provider, external id, version, content fingerprint, last synced). A `RecurrenceIdUtc` becomes an override of its series.
3. **Conflicts** (two-way). An event whose fingerprint changed here and whose external version also moved is settled by `ConflictPolicy`: `ExternalWins` (default) overwrites the local change; `LocalWins` keeps it and writes it back (re-creating an externally deleted event).
4. **Write back** (two-way, `IWritableCalendarProvider`). `PendingDeletes` first, then every event without a mapping or with a changed fingerprint is pushed; series before their occurrence changes.
5. **Record** `LastSyncedUtc`, `LastResult`, `LastError`, `Failures`. A thrown exception increments `Failures` and leaves existing events untouched.

The `calendars-external-sync` health check (tag `calendars`) reports **Degraded** while a subscription needs reconnecting or has failed three times in a row.

## The Contracts

```csharp
public interface ICalendarProvider
{
    string Name { get; }
    LocalizationSource DisplayName { get; }
    bool UsesAddress => true;
    bool IsConfigured(Calendar calendar, CalendarSubscription subscription)
        => !string.IsNullOrWhiteSpace(subscription?.Url);
    Task<IReadOnlyList<ICalendarEventData>> PullAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default);
}

public interface IIncrementalCalendarProvider : ICalendarProvider
{
    Task<CalendarProviderChanges> PullChangesAsync(Calendar calendar, CalendarSubscription subscription, string syncToken, CancellationToken cancellationToken = default);
    Task<string> GetSyncTokenAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default);
}

public interface IWritableCalendarProvider : ICalendarProvider
{
    Task<CalendarProviderPushResult> PushAsync(CalendarProviderPushContext context, CancellationToken cancellationToken = default);
    Task DeleteAsync(CalendarProviderPushContext context, CancellationToken cancellationToken = default);
}
```

| Member | Rule |
|---|---|
| `Name` | Stable technical name stored on each subscription (`google`, `microsoft365`); never rename. |
| `PullAsync` | Every event in the sync window. Events left out are removed here, so page to the end or throw. |
| `UsesAddress` / `IsConfigured` | Account providers return `false` / whether a connected account and calendar exist. Unconfigured subscriptions are never due. |
| `PullChangesAsync` | `CalendarProviderChanges` with `Events`, `DeletedIds`, `NextSyncToken`; set `FullSyncRequired = true` for an expired token instead of throwing. |
| `PushAsync` | `context.Mapping == null` creates; otherwise update `Mapping.ExternalEventId` with `Mapping.Version` as the precondition (If-Match). Return the external id and the **new** version. `context.SeriesMapping` addresses an occurrence's series. |
| `DeleteAsync` | Delete `context.Mapping.ExternalEventId` (`context.Event` is null); already gone is success. |
| `CalendarProviderConflictException` | Throw on a failed precondition (HTTP 412); never resolve conflicts yourself. |
| `CalendarProviderAuthorizationException` | Throw when access is refused for good (revoked consent, `invalid_grant`); the subscription pauses with `NeedsReconnect`. |

Everything else (timeouts, 429, 5xx) should simply throw; the service backs off.

**Identity.** `ICalendarEventData.Uid` must be the external event id, the same value `PushAsync` returns as `ExternalEventId`. For a changed occurrence, return the series' id as `Uid` and its original start in UTC as `RecurrenceIdUtc`. Put the external ETag or change key in `Version` on every read.

## A Minimal Read-Only Provider

```csharp
public sealed class PartnerCalendarProvider : ICalendarProvider
{
    public const string ProviderName = "partner";

    private readonly IHttpClientFactory _httpClientFactory;

    public PartnerCalendarProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Name => ProviderName;

    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<PartnerCalendarProvider>("Partner calendar");

    public async Task<IReadOnlyList<ICalendarEventData>> PullAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(ProviderName);
        var items = await client.GetFromJsonAsync<List<PartnerEvent>>(subscription.Url, cancellationToken) ?? [];

        return items.Select(item => new ICalendarEventData
        {
            Uid = item.Id,
            Version = item.ETag,
            Summary = item.Title,
            Location = item.Room,
            Start = item.LocalStart,          // wall-clock in TimeZoneId, DateTimeKind.Unspecified
            End = item.LocalEnd,
            TimeZoneId = item.TimeZone,       // IANA, such as America/New_York
            Rule = item.RepeatRule,           // RRULE value without the "RRULE:" prefix
            Status = CalendarEventStatus.Confirmed,
            Transparency = CalendarTransparency.Busy,
        }).ToList();
    }
}
```

```csharp
[Feature("MyCompany.Calendars.Partner")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(PartnerCalendarProvider.ProviderName);
        services.AddCalendarProvider<PartnerCalendarProvider>(); // also registers CalendarSyncService, the importer and the change observer
    }
}
```

Give the feature a dependency on `CrestApps.OrchardCore.Calendars.External` and reference `CrestApps.OrchardCore.Calendars.Core`. Never register `CalendarSyncService`, the importer or the observer yourself, and never use `new HttpClient()`.

## Mapping Rules

- Read and write exactly the fingerprinted fields (`CalendarSyncFingerprint`): title, description, location, all-day, start, end, time zone, status, transparency, rule, exception dates, attendee emails. A field written but never read back makes the event look changed every run.
- `Start`/`End` are wall-clock in `TimeZoneId` (IANA; convert Windows names with `TimeZoneInfo.TryConvertWindowsIdToIanaId`). All-day: midnight of the first date and the day after the last (exclusive).
- Don't send invitations from the provider; prefer the API option that suppresses notifications on sync writes.
- Read a bounded window (for example one year back, two ahead); an event leaving the window on a full read is removed.

## Account Providers (Google, Microsoft 365)

- OAuth 2.0 authorization code with PKCE and a `state` bound to the calendar and user, in your own admin controller; require **Manage** on the calendar (`ICalendarAccessService`).
- App credentials (client id, secret) in site settings or configuration, the secret protected with `IDataProtectionProvider`; never in code.
- Keep the per-calendar connection (account, external calendar id, protected refresh token) as a typed object in the calendar's properties (`calendar.Put(new PartnerConnection { ... })`); never log tokens or put them in `LastError`.
- Add a `DisplayDriver<Calendar>` that shows Connect/Reconnect and an account calendar picker only when `CalendarSubscription.Provider == Name`. The shared **Subscription** tab already shows the provider picker, interval, two-way switch (only for writable providers), conflict policy and last result.
- Push notifications (Google watch channels, Graph subscriptions) are optional: validate them, set `CalendarSubscription.LastAttemptUtc = null`, save with `ICalendarService.UpdateAsync`, and never sync inside the webhook request.

## Tests to Write

- Mapping fixtures: timed, all-day, a series with an exception and a moved occurrence, a cancelled occurrence, time zones, attendees.
- A round trip: what `PushAsync` wrote, read back, gives the same fingerprint.
- HTTP behavior with a fake handler: paging to the end, sync tokens, expired token gives `FullSyncRequired`, 412 gives the conflict exception, 401/`invalid_grant` gives the authorization exception, 404 on delete succeeds, 429/5xx throw.

## Checklist

- [ ] `Name`, `DisplayName`; `UsesAddress` and `IsConfigured` for account providers.
- [ ] `PullAsync` pages to the end; `Uid` is the external id; `Version` set.
- [ ] Incremental reads with `FullSyncRequired` when the API supports tokens.
- [ ] `PushAsync`/`DeleteAsync` with If-Match; both exceptions; 404 delete is success.
- [ ] Wall-clock times with IANA zones; all-day dates; recurrence and occurrence changes.
- [ ] OAuth with PKCE and `state`; protected tokens; Manage permission on connect.
- [ ] Registered with `services.AddCalendarProvider<T>()`.
