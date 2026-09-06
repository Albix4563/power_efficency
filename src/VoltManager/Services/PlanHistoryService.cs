using System.Collections.ObjectModel;
using VoltManager.Models;

namespace VoltManager.Services;

public enum PlanHistoryCategory
{
    Automatic,
    Manual,
    External,
}

public enum PlanHistoryOutcome
{
    Applied,
    ExternalDetected,
    Failed,
    Unverifiable,
}

public sealed record PlanHistoryPlan(string Guid, string Name, PlanId? PlanId);

public sealed record PlanChangeContext(
    PlanHistoryCategory Category,
    string Source,
    string ReasonCode,
    IReadOnlyDictionary<string, string> Details);

public sealed record PlanHistoryEntry(
    long Id,
    DateTime FirstTimestampUtc,
    DateTime LastTimestampUtc,
    int Attempts,
    PlanHistoryCategory Category,
    string Source,
    string ReasonCode,
    IReadOnlyDictionary<string, string> Details,
    PlanHistoryPlan? PreviousPlan,
    PlanHistoryPlan? RequestedPlan,
    PlanHistoryPlan? ObservedPlan,
    PlanHistoryOutcome Outcome);

public sealed record PlanHistorySnapshot(long Revision, IReadOnlyList<PlanHistoryEntry> Entries);

public sealed class PlanHistoryService
{
    public const int Capacity = 500;
    private static readonly TimeSpan ProblemGroupingWindow = TimeSpan.FromSeconds(30);

    private readonly object _lock = new();
    private readonly List<PlanHistoryEntry> _entries = new(Capacity);
    private long _nextId;
    private long _revision;

    public event Action<long>? Changed;

    public PlanHistorySnapshot GetSnapshot()
    {
        lock (_lock)
            return new PlanHistorySnapshot(_revision, _entries.AsEnumerable().Reverse().ToArray());
    }

    public long Clear()
    {
        long revision;
        lock (_lock)
        {
            _entries.Clear();
            revision = ++_revision;
        }
        Changed?.Invoke(revision);
        return revision;
    }

    internal long Record(
        DateTime timestampUtc,
        PlanChangeContext context,
        PlanHistoryPlan? previous,
        PlanHistoryPlan? requested,
        PlanHistoryPlan? observed,
        PlanHistoryOutcome outcome)
    {
        lock (_lock)
        {
            if (IsProblem(outcome) && _entries.Count > 0)
            {
                var last = _entries[^1];
                if (IsSameProblem(last, timestampUtc, context, previous, requested, observed, outcome))
                {
                    _entries[^1] = last with
                    {
                        LastTimestampUtc = timestampUtc,
                        Attempts = last.Attempts + 1,
                    };
                    return ++_revision;
                }
            }

            _entries.Add(new PlanHistoryEntry(
                ++_nextId,
                timestampUtc,
                timestampUtc,
                1,
                context.Category,
                context.Source,
                context.ReasonCode,
                new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(context.Details, StringComparer.Ordinal)),
                previous,
                requested,
                observed,
                outcome));

            if (_entries.Count > Capacity)
                _entries.RemoveAt(0);

            return ++_revision;
        }
    }

    internal void PublishChanged(long revision) => Changed?.Invoke(revision);

    private static bool IsProblem(PlanHistoryOutcome outcome)
        => outcome is PlanHistoryOutcome.Failed or PlanHistoryOutcome.Unverifiable;

    private static bool IsSameProblem(
        PlanHistoryEntry last,
        DateTime timestampUtc,
        PlanChangeContext context,
        PlanHistoryPlan? previous,
        PlanHistoryPlan? requested,
        PlanHistoryPlan? observed,
        PlanHistoryOutcome outcome)
        => IsProblem(last.Outcome)
           && timestampUtc - last.LastTimestampUtc <= ProblemGroupingWindow
           && last.Category == context.Category
           && last.Source == context.Source
           && last.ReasonCode == context.ReasonCode
           && last.Outcome == outcome
           && last.PreviousPlan == previous
           && last.RequestedPlan == requested
           && last.ObservedPlan == observed
           && DictionariesEqual(last.Details, context.Details);

    private static bool DictionariesEqual(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var value) && value == kv.Value);
}
