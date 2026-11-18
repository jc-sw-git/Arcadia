namespace Arcadia.App.Services;

/// <summary>
/// Gives back the memory a burst of work left behind (#636): a scan, the artwork and metadata runs, a sync. Their garbage
/// stays in the process until the GC runs, and at idle nothing makes it run: on a desktop CPU the first generation takes
/// tens of megabytes before it collects, and the old ones far more. Once enough was allocated since the last trim and the
/// player has left Arcadia alone for a moment, one full, compacting collection hands the free memory back to Windows.
/// The collection pauses every thread for a moment (about 200 ms with 5,000 games): never while the player presses, and
/// never while a game runs or Arcadia isn't in front, where it would stall the pads' forwarding (budget: 4 ms). A trim
/// put off then follows once Arcadia is back in front.
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

    /// <summary>The player has pressed nothing for <see cref="QuietTime"/>: views may let go of what is out of sight (#685).</summary>
    public bool IsQuiet => time.GetUtcNow() - lastInput() >= QuietTime;

    /// <summary>
    /// Trims if it is worth it and the player is quiet; true if it trimmed. <paramref name="mayPause"/>: Arcadia is in
    /// front and no game runs, so a pause stalls nothing the player plays with. <paramref name="viewsLetGo"/>: views
    /// just let go of controls out of sight (#685); they were long-lived, so only a full collection gives them back,
    /// however little was allocated.
    /// </summary>
    public bool TrimIfDue(bool mayPause, bool viewsLetGo = false)
    {
        if (!mayPause || !IsQuiet || (!viewsLetGo && allocatedBytes() - _allocatedAtLastTrim < WorthTrimming))
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
