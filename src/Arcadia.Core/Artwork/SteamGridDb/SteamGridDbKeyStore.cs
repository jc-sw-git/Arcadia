using Arcadia.Core.Settings;

namespace Arcadia.Core.Artwork.SteamGridDb;

/// <summary>
/// Keeps the SteamGridDB key (#489) in <c>steamgriddb-key.dat</c> in Arcadia's data folder, encrypted as a
/// <see cref="SecretFile"/>; a stored value that isn't a key counts as no key.
/// </summary>
/// <param name="protect">Encrypts the key; null keeps it in memory only.</param>
public sealed class SteamGridDbKeyStore(string folder, Func<byte[], byte[]>? protect, Func<byte[], byte[]>? unprotect)
{
    public const string FileName = "steamgriddb-key.dat";

    private readonly SecretFile _file = new(folder, FileName, "SteamGridDB", protect, unprotect);

    public string? Load() => SteamGridDbKey.Normalize(_file.Load());

    /// <summary>Keeps <paramref name="key"/>; null deletes it. False if it couldn't be written (the key then lasts until Arcadia closes).</summary>
    public bool Save(string? key) => _file.Save(key);
}
