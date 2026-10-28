using Arcadia.Launchers;

namespace Arcadia.App.Services;

/// <summary>
/// A window of Steam itself (the client, Big Picture, its overlay windows) is in front, not a game (#242): there the
/// Steam Controller belongs to Steam while no game runs. Asked on every gamepad poll, so the answer is kept for a moment.
/// </summary>
public sealed class SteamInFront(TimeProvider time)
{
    private static readonly TimeSpan Refresh = TimeSpan.FromMilliseconds(250);
    private static readonly string[] SteamPrograms = ["steam.exe", "steamwebhelper.exe"];

    private long _askedAt = long.MinValue;
    private volatile bool _inFront;

    public bool Now()
    {
        var now = time.GetTimestamp();
        if (_askedAt != long.MinValue && time.GetElapsedTime(Interlocked.Read(ref _askedAt), now) < Refresh)
        {
            return _inFront;
        }

        Interlocked.Exchange(ref _askedAt, now);
        _inFront = ProcessWatch.ForegroundProcessId() is { } id
            && ProcessWatch.PathOf(id) is { } path
            && SteamPrograms.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        return _inFront;
    }
}
