# Observability Examples

These examples add a new instrument and span to CrestApps.Core code, then test them. `crestapps.core.ai.summary.duration` and `crestapps.chat.summarize` are example names; they do not exist in the framework.

## Declare the Instrument

```csharp
// In AITelemetry (CrestApps.Core.AI.Abstractions), next to the other instruments.

/// <summary>
/// The duration of summarizing one conversation, tagged with <see cref="CrestAppsTelemetry.OutcomeTag"/>.
/// </summary>
public static readonly Histogram<double> SummaryDuration = CrestAppsTelemetry.CreateDurationHistogram(
    "crestapps.core.ai.summary.duration",
    "The duration of summarizing one conversation.");
```

## Trace and Measure the Work

The span carries the session id. The metric carries only the bounded `outcome` tag.

```csharp
using System.Diagnostics;
using CrestApps.Core;
using CrestApps.Core.AI;

public sealed class ConversationSummarizer
{
    public async Task<string> SummarizeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        using var activity = CrestAppsTelemetry.ActivitySource.StartActivity("crestapps.chat.summarize");
        activity?.SetTag(AITelemetry.SessionIdAttribute, sessionId);

        var startTimestamp = Stopwatch.GetTimestamp();
        var outcome = CrestAppsTelemetry.OutcomeOk;

        try
        {
            return await CreateSummaryAsync(sessionId, cancellationToken);
        }
        catch (Exception ex)
        {
            outcome = CrestAppsTelemetry.GetOutcome(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message).AddException(ex);

            throw;
        }
        finally
        {
            AITelemetry.SummaryDuration.Record(
                Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                new KeyValuePair<string, object>(CrestAppsTelemetry.OutcomeTag, outcome));
        }
    }

    private static Task<string> CreateSummaryAsync(string sessionId, CancellationToken cancellationToken)
        => Task.FromResult(string.Empty);
}
```

## Test It

The test listens with `MeterListener` and `ActivityListener`, so it needs no OpenTelemetry SDK. The span is matched by a session id that only this test uses, because other tests record into the same meter and source at the same time.

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.Core;
using CrestApps.Core.AI;

public sealed class ConversationSummarizerTelemetryTests
{
    [Fact]
    public async Task SummarizeAsync_RecordsASpanAndItsDuration()
    {
        var measurements = new ConcurrentQueue<string>();
        var activities = new ConcurrentQueue<Activity>();

        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == CrestAppsTelemetry.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => measurements.Enqueue(instrument.Name));
        meterListener.Start();

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CrestAppsTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Enqueue,
        };

        ActivitySource.AddActivityListener(activityListener);

        var sessionId = Guid.NewGuid().ToString("N");

        await new ConversationSummarizer().SummarizeAsync(sessionId, TestContext.Current.CancellationToken);

        Assert.Contains(activities, activity => activity.OperationName == "crestapps.chat.summarize" && Equals(activity.GetTagItem(AITelemetry.SessionIdAttribute), sessionId));
        Assert.Contains("crestapps.core.ai.summary.duration", measurements);
    }
}
```

In the CrestApps.Core repository, also add the name to `_expectedInstrumentNames` in `tests/CrestApps.Core.Tests/Core/Telemetry/CrestAppsTelemetryTests.cs`, and reuse the `TelemetryCollector` in the same folder instead of new listeners.
