namespace Arcadia.App.Services;

/// <summary>
/// Gives back the memory a burst of work left behind (#636): a scan, the artwork and metadata runs, a sync. Their garbage
/// stays in the process until the GC runs, and at idle nothing makes it run: on a desktop CPU the first generation takes
/// tens of megabytes before it collects, and the old ones far more. Once enough was allocated since the last trim and the
/// player has left Arcadia alone for a moment, one full, compacting collection hands the free memory back to Windows.
/// Never while the player presses: the collection pauses every thread for a moment.
/// </summary>
public sealed class MemoryTrim(TimeProvider time, Func<DateTimeOffset> lastInput, Func<long> allocatedBytes, Action collect)
{
    /// <summary>How often the timer asks.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    /// <summary>How long the player has pressed nothing before a trim may pause Arcadia.</summary>
    public static readonly TimeSpan QuietTime = TimeSpan.FromSeconds(10);

    /// <summary>Less than this since the last trim isn't worth a pause; idle alone allocates about a megabyte a minute.</summary>
    public const long WorthTrimming = 64L * 1024 * 1024;

    private long _allocatedAtLastTrim;

    /// <summary>The trim of the running process: a full, blocking, compacting collection that also decommits what it frees.</summary>
    public static MemoryTrim ForProcess(TimeProvider time, Func<DateTimeOffset> lastInput) =>
        new(time, lastInput, () => GC.GetTotalAllocatedBytes(), () => GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true));

    /// <summary>Trims if it is worth it and the player is quiet; true if it trimmed.</summary>
    public bool TrimIfDue()
    {
        if (time.GetUtcNow() - lastInput() < QuietTime || allocatedBytes() - _allocatedAtLastTrim < WorthTrimming)
        {
            return false;
        }

        var before = GC.GetTotalMemory(forceFullCollection: false);
        var started = time.GetTimestamp();
        collect();
        _allocatedAtLastTrim = allocatedBytes();
        AppLog.Write($"Speicher: aufgeräumt in {time.GetElapsedTime(started).TotalMilliseconds:F0} ms, verwaltet {before / 1_048_576} → {GC.GetTotalMemory(forceFullCollection: false) / 1_048_576} MB"); // not a UI text (log)
        return true;
    }
}
