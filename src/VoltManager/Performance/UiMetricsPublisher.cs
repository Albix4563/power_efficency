using VoltManager.Models;

namespace VoltManager.Performance;

/// <summary>
/// Latest-value coalescer for WebView telemetry. Incoming samples always replace the
/// pending snapshot; no queue can build up while UI publishing is throttled.
/// </summary>
public sealed class UiMetricsPublisher
{
    private readonly object _gate = new();
    private MetricsSnapshot? _latest;
    private DateTime? _lastPublishedUtc;
    private object? _queuedValue;
    private bool _dispatchPending;

    /// <summary>At most one waiting UI callback; a busy renderer cannot block the sampler or build a queue.</summary>
    public void QueueLatest(object value, Action<Action> enqueue, Action<object> publish)
    {
        lock (_gate)
        {
            _queuedValue = value;
            if (_dispatchPending) return;
            _dispatchPending = true;
        }
        try
        {
            enqueue(() =>
            {
                object? latest;
                lock (_gate)
                {
                    latest = _queuedValue;
                    _queuedValue = null;
                    _dispatchPending = false;
                }
                if (latest != null) publish(latest);
            });
        }
        catch
        {
            lock (_gate) _dispatchPending = false;
            throw;
        }
    }

    public bool TryTake(
        MetricsSnapshot incoming,
        WebViewResourcePlan plan,
        DateTime nowUtc,
        out MetricsSnapshot? snapshot)
    {
        lock (_gate)
        {
            _latest = incoming;
            snapshot = null;
            if (!plan.PublishMetrics) return false;

            if (_lastPublishedUtc is DateTime last && nowUtc - last < plan.MetricsInterval)
                return false;

            _lastPublishedUtc = nowUtc;
            snapshot = _latest;
            return true;
        }
    }

    /// <summary>Make the next visible sample publish immediately after a profile/visibility transition.</summary>
    public void ResetCadence()
    {
        lock (_gate) _lastPublishedUtc = null;
    }
}
