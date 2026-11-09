# Outreach Extensions

The examples send a newsletter to subscribers kept by your own module. `NewsletterSubscriberIndex`, `Newsletter` and `INewsletterStore` stand for your own index, record and store. Your feature must depend on `CrestApps.OrchardCore.Outreach` (and `OrchardCore.Email` for the `Email` sender).

## Registration

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOutreachAudienceSource<NewsletterAudienceSource>()
            .AddOutreachTemplateRenderer<NewsletterTemplateRenderer>()
            .AddOutreachRecipientPolicy<BlockedDomainPolicy>()
            .AddOutreachRunObserver<NewsletterRunObserver>();

        services.AddScoped<NewsletterSender>();
    }
}
```

## Audience Source

Page with a cursor that only moves forward, and never return a recipient on two pages: the engine de-duplicates keys within one batch only.

```csharp
public sealed class NewsletterAudienceSource : IOutreachAudienceSource
{
    public const string SourceName = "NewsletterSubscribers";

    private readonly ISession _session;

    public NewsletterAudienceSource(ISession session)
    {
        _session = session;
    }

    public string Name => SourceName;

    public async Task<OutreachAudiencePage> GetPageAsync(OutreachRun run, string cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        var after = long.TryParse(cursor, CultureInfo.InvariantCulture, out var value) ? value : 0L;

        var rows = (await _session.QueryIndex<NewsletterSubscriberIndex>(index => index.DocumentId > after)
            .OrderBy(index => index.DocumentId)
            .Take(pageSize)
            .ListAsync(cancellationToken)).ToList();

        return new OutreachAudiencePage
        {
            Recipients = [.. rows.Select(row => new OutreachRecipient
            {
                Key = row.DocumentId.ToString(CultureInfo.InvariantCulture),
                Address = row.Email,
                DisplayName = row.Name,

                // The engine counts it as skipped and never sends.
                SkipReason = row.Unsubscribed ? OutreachConstants.SkipReasons.OptedOut : null,
            })],

            // Null on the last page completes the run.
            NextCursor = rows.Count < pageSize ? null : rows[^1].DocumentId.ToString(CultureInfo.InvariantCulture),
        };
    }
}
```

## Template Renderer

Without a renderer the run sends its `Template` as it is (`Text`). A renderer personalizes it per recipient.

```csharp
public sealed class NewsletterTemplateRenderer : IOutreachTemplateRenderer
{
    public const string RendererName = "Newsletter";

    public string Name => RendererName;

    public Task<OutreachContent> RenderAsync(OutreachRun run, OutreachRecipient recipient, CancellationToken cancellationToken = default)
    {
        var name = WebUtility.HtmlEncode(recipient.DisplayName ?? "there");

        return Task.FromResult(new OutreachContent
        {
            Subject = run.Template.Subject,
            Body = run.Template.Body?.Replace("{name}", name, StringComparison.Ordinal),
            IsHtml = run.Template.IsHtml,
        });
    }
}
```

## Recipient Policy

Consent is already checked when the run names a `ConsentChannel`. Add a policy for a rule the consent service does not know. A policy applies to every run, so check the run first.

```csharp
public sealed class BlockedDomainPolicy : IOutreachRecipientPolicy
{
    public Task<string> GetSkipReasonAsync(OutreachRun run, OutreachRecipient recipient, CancellationToken cancellationToken = default)
    {
        if (run.Sender != OutreachConstants.EmailSender ||
            recipient.Address?.EndsWith("@example.invalid", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Task.FromResult<string>(null);
        }

        return Task.FromResult("BlockedDomain");
    }
}
```

## Run Observer

Called after each batch and when the run fails, inside the batch's transaction. Copy the counters onto your record and save it, but never commit. Filter on `OwnerType`, since every observer sees every run.

```csharp
public sealed class NewsletterRunObserver : IOutreachRunObserver
{
    public const string OwnerType = "Newsletter";

    private readonly INewsletterStore _newsletters;

    public NewsletterRunObserver(INewsletterStore newsletters)
    {
        _newsletters = newsletters;
    }

    public async Task RunUpdatedAsync(OutreachRun run, CancellationToken cancellationToken = default)
    {
        if (run.OwnerType != OwnerType ||
            await _newsletters.FindByIdAsync(run.OwnerId, cancellationToken) is not { } newsletter)
        {
            return;
        }

        newsletter.Sent = run.Sent;
        newsletter.Skipped = run.Skipped;
        newsletter.Failed = run.Failed;
        newsletter.Status = run.Status.ToString();

        await _newsletters.UpdateAsync(newsletter, cancellationToken);
    }
}
```

## Start and Cancel a Run

`StartAsync` writes the run and its first batch into the current session, so they commit with your own change. Store the run id on your record to avoid starting twice.

```csharp
public sealed class NewsletterSender
{
    private readonly IOutreachEngine _outreachEngine;
    private readonly INewsletterStore _newsletters;

    public NewsletterSender(IOutreachEngine outreachEngine, INewsletterStore newsletters)
    {
        _outreachEngine = outreachEngine;
        _newsletters = newsletters;
    }

    public async Task SendAsync(Newsletter newsletter, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(newsletter.OutreachRunId))
        {
            return;
        }

        var run = await _outreachEngine.StartAsync(new OutreachRun
        {
            Name = newsletter.Title,
            OwnerType = NewsletterRunObserver.OwnerType,
            OwnerId = newsletter.ItemId,
            AudienceSource = NewsletterAudienceSource.SourceName,
            Sender = OutreachConstants.EmailSender,
            SenderAddress = "news@example.com",
            Renderer = NewsletterTemplateRenderer.RendererName,
            ConsentChannel = ConsentChannels.Email,
            ConsentContactKind = ConsentContactKinds.Customer,
            Template = new OutreachTemplate { Subject = newsletter.Subject, Body = newsletter.Body, IsHtml = true },
        }, cancellationToken);

        newsletter.OutreachRunId = run.ItemId;
        await _newsletters.UpdateAsync(newsletter, cancellationToken);
    }

    public async Task CancelAsync(Newsletter newsletter, CancellationToken cancellationToken = default)
    {
        // Observers are not called on cancel, so update the record here.
        if (await _outreachEngine.CancelAsync(newsletter.OutreachRunId, cancellationToken))
        {
            newsletter.Status = "Canceled";
            await _newsletters.UpdateAsync(newsletter, cancellationToken);
        }
    }
}
```

For a text channel, set `SendingHoursScheduleId` to a Business Hours schedule and use a sender whose `ObservesQuietHours` returns true; batches then wait for the schedule to open without using attempts.

## Your Own Leased Work Queue

Use the same building block for other durable work that several nodes drain together.

```csharp
public sealed class ExportJob : CatalogItem, ILeasedWorkItem
{
    public string Format { get; set; }

    public LeasedWorkItemStatus Status { get; set; }

    public string OwnerToken { get; set; }

    public long FenceToken { get; set; }

    public int AttemptCount { get; set; }

    public DateTime NextAttemptUtc { get; set; }

    public string LastError { get; set; }
}

public sealed class ExportsStartup : StartupBase
{
    public const string Collection = "Exports";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(Collection));
        services.AddLeasedWorkQueue<ExportJob>(options =>
        {
            options.CollectionName = Collection;
            options.MaxAttempts = 5;
            options.RemoveCompletedItems = true;
        });
        services.AddDataMigration<ExportsMigrations>();
    }
}

public sealed class ExportsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await LeasedWorkItemIndexSchema.CreateAsync(SchemaBuilder, ExportsStartup.Collection);

        return 1;
    }
}

public sealed class ExportWorker
{
    private static readonly Func<int, TimeSpan> _backoff = LeasedWorkBackoff.Exponential(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));

    private readonly ILeasedWorkQueue<ExportJob> _queue;

    public ExportWorker(ILeasedWorkQueue<ExportJob> queue)
    {
        _queue = queue;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach (var job in await _queue.ClaimDueAsync(10, TimeSpan.FromMinutes(5), cancellationToken))
        {
            try
            {
                // Do the work here.
                await _queue.CompleteAsync(job, cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                // Pending again after the back-off, or dead-lettered once MaxAttempts is used.
                await _queue.RetryAsync(job, exception.Message, _backoff, cancellationToken: cancellationToken);
            }
        }
    }
}
```

- `EnqueueAsync(job)` joins the caller's session: the job is stored only when that session commits.
- `ClaimDueAsync`, `CompleteAsync`, `RetryAsync` and `AbandonAsync` on `LeasedWorkQueue<T>` commit in sessions of their own; call them while your session has no unsaved writes.
- `RetryAsync(..., countAttempt: false)` waits without using an attempt, for work that could not even start.
