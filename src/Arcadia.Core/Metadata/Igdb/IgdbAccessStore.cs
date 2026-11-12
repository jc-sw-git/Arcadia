using Arcadia.Core.Settings;

namespace Arcadia.Core.Metadata.Igdb;

/// <summary>
/// Keeps the IGDB access (#494), Twitch's client id and secret, in <c>igdb-access.dat</c> in Arcadia's data folder,
/// encrypted as a <see cref="SecretFile"/>. A stored value that isn't one (<see cref="IgdbAccess.Normalize"/>) counts as
/// missing.
/// </summary>
public sealed class IgdbAccessStore(string folder, Func<byte[], byte[]>? protect, Func<byte[], byte[]>? unprotect)
{
    public const string FileName = "igdb-access.dat";

    private readonly SecretFile _file = new(folder, FileName, "IGDB", protect, unprotect);

    /// <summary>The access is kept across a restart (Windows); else only until Arcadia closes.</summary>
    public bool Persists => _file.Persists;

    public (string? ClientId, string? Secret) Load()
    {
        var lines = (_file.Load() ?? string.Empty).Split('\n');
        return (IgdbAccess.Normalize(lines[0]), lines.Length > 1 ? IgdbAccess.Normalize(lines[1]) : null);
    }

    /// <summary>Keeps both values (either may be missing); neither: the file is deleted. False if it couldn't be written.</summary>
    public bool Save(string? clientId, string? secret) =>
        _file.Save(clientId is null && secret is null ? null : $"{clientId}\n{secret}");
}
