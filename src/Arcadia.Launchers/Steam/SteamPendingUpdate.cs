namespace Arcadia.Launchers.Steam;

/// <summary>
/// Whether Steam has an update for an installed app (#288). <c>StateFlags</c> bit 2 alone isn't enough: at a Steam start
/// Steam sets it for a few seconds on apps with nothing new (Half-Life 2 after its merged episodes, verified in the
/// owner's content log: same build, 0 bytes to download) and drops it again. A real update has a newer
/// <c>TargetBuildID</c> or bytes still to download; after a finished update <c>BytesToDownload</c> stays in the manifest,
/// equal to <c>BytesDownloaded</c>. A manifest without <c>TargetBuildID</c> (older ones) leaves the flag to decide.
/// </summary>
public static class SteamPendingUpdate
{
    private const int UpdateRequired = 2;

    public static bool IsPending(int stateFlags, long buildId, long targetBuildId, long bytesToDownload, long bytesDownloaded) =>
        (stateFlags & UpdateRequired) != 0
        && (targetBuildId <= 0 || targetBuildId != buildId || bytesDownloaded < bytesToDownload);
}
