using VoltManager.Models;
using VoltManager.Services;

namespace VoltManager.Performance;

/// <summary>On-demand process deltas. Concurrent views share one bounded, throttled sample.</summary>
internal sealed class ProcessMetricsSampler(Func<ProcessSnapshot> capture, int logicalCores)
{
    private readonly object _gate = new();
    private Dictionary<(int Pid, DateTime? Started), TimeSpan> _previous = new();
    private Dictionary<(int Pid, DateTime? Started), TimeSpan> _spare = new();
    private DateTime _sampledAtUtc;
    private DateTime? _requestedAtUtc;
    private List<ProcessInfo> _cached = new();

    public List<ProcessInfo> Read(int count, TimeSpan interval, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (interval != Timeout.InfiniteTimeSpan &&
                (_requestedAtUtc == null || nowUtc < _requestedAtUtc || nowUtc - _requestedAtUtc >= interval))
            {
                // Also throttle a failing provider: repeated RPCs must not cause a retry storm.
                _requestedAtUtc = nowUtc;
                Update(capture());
            }
            return _cached.Take(Math.Clamp(count, 0, 12)).ToList();
        }
    }

    private void Update(ProcessSnapshot snapshot)
    {
        if (snapshot.TakenUtc == _sampledAtUtc) return;
        double elapsed = (snapshot.TakenUtc - _sampledAtUtc).TotalSeconds;
        bool hasPrevious = elapsed > 0 && elapsed < 30;
        _spare.Clear();
        var results = new List<(string Name, int Pid, double CpuPct, double RamMb)>(snapshot.Processes.Length);
        foreach (var process in snapshot.Processes)
        {
            if (process.Pid == 0) continue;
            var identity = (process.Pid, process.StartTimeUtc);
            _spare[identity] = process.CpuTime;
            double cpu = hasPrevious && _previous.TryGetValue(identity, out var previous)
                ? Math.Clamp((process.CpuTime - previous).TotalSeconds / elapsed / Math.Max(1, logicalCores) * 100, 0, 100)
                : 0;
            results.Add((process.Name, process.Pid, cpu, process.WorkingSetBytes / (1024.0 * 1024)));
        }
        (_previous, _spare) = (_spare, _previous);
        _sampledAtUtc = snapshot.TakenUtc;
        _cached = results
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProcessInfo
            {
                Name = g.Key,
                Pid = g.First().Pid,
                CpuPercent = Math.Round(g.Sum(r => r.CpuPct), 1),
                RamMb = Math.Round(g.Sum(r => r.RamMb), 0),
                Instances = g.Count(),
            })
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.RamMb)
            .Take(12)
            .ToList();
    }
}
