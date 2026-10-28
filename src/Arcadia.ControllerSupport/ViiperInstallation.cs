using System.Security.Cryptography;

namespace Arcadia.ControllerSupport;

/// <summary>The <c>viiper.exe</c> next to Arcadia: there, the pinned build, and fetched again when it went missing.</summary>
public interface IViiperInstallation
{
    bool IsInstalled { get; }

    /// <summary>
    /// Downloads the pinned build next to Arcadia and checks its SHA-256 (<see cref="VerifiedDownload"/>); no
    /// administrator rights, as Arcadia's folder is the user's own. Throws when nothing is pinned or the check fails.
    /// </summary>
    Task InstallAsync(HttpClient http, CancellationToken cancellationToken);
}

/// <param name="executable">Where <c>viiper.exe</c> lives: next to Arcadia.exe (<see cref="NextToArcadia"/>).</param>
public sealed class ViiperInstallation(string executable, ReleaseAsset pinned) : IViiperInstallation
{
    private readonly Lock _hashLock = new();
    private (long Length, DateTime WrittenUtc, bool Pinned)? _lastHash;

    public static ViiperInstallation NextToArcadia() =>
        new(Path.Combine(AppContext.BaseDirectory, ViiperRelease.ExecutableName), ViiperRelease.Asset);

    public string Executable => executable;

    public bool IsInstalled => File.Exists(executable);

    /// <summary>
    /// The file is the pinned build. Another <c>viiper.exe</c> (an upstream one put there by hand) lacks the copy's device
    /// type, and the copy would leave the puck hidden with nothing in its place. The hash is cached per size and write
    /// time: the emulation asks every 2 s.
    /// </summary>
    public bool IsPinned()
    {
        if (pinned.Sha256 is not { } expected)
        {
            return false;
        }

        try
        {
            var info = new FileInfo(executable);
            if (!info.Exists)
            {
                return false;
            }

            lock (_hashLock)
            {
                if (_lastHash is { } last && last.Length == info.Length && last.WrittenUtc == info.LastWriteTimeUtc)
                {
                    return last.Pinned;
                }

                using var file = File.OpenRead(executable);
                var matches = string.Equals(Convert.ToHexStringLower(SHA256.HashData(file)), expected, StringComparison.OrdinalIgnoreCase);
                _lastHash = (info.Length, info.LastWriteTimeUtc, matches);
                return matches;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task InstallAsync(HttpClient http, CancellationToken cancellationToken)
    {
        if (pinned.Sha256 is null)
        {
            throw new InvalidOperationException("Diese Arcadia-Version hat keine geprüfte VIIPER-Version.");
        }

        // Downloaded beside it and moved only once checked, so a viiper.exe that failed the check never sits where it runs.
        var download = executable + ".download";
        await VerifiedDownload.DownloadAsync(http, pinned, download, cancellationToken);
        File.Move(download, executable, overwrite: true);
    }
}
