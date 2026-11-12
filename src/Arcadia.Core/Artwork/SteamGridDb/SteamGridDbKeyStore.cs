using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Arcadia.Core.Artwork.SteamGridDb;

/// <summary>
/// Keeps the SteamGridDB key (#489) in <c>steamgriddb-key.dat</c> in Arcadia's data folder, encrypted by
/// <c>protect</c> (DPAPI for the current Windows user, handed in by the App). Without it (not Windows) the key lasts only
/// until Arcadia closes, like the Steam sign-in, and nothing about it is written. A file that can't be read or decrypted
/// counts as no key; reading and writing never throw, and no log line holds the key.
/// </summary>
/// <param name="protect">Encrypts the key; null keeps it in memory only.</param>
public sealed class SteamGridDbKeyStore(string folder, Func<byte[], byte[]>? protect, Func<byte[], byte[]>? unprotect)
{
    public const string FileName = "steamgriddb-key.dat";

    private readonly Lock _lock = new();
    private string? _inMemory;

    private string KeyPath => Path.Combine(folder, FileName);

    private bool Persists => protect is not null && unprotect is not null;

    public string? Load()
    {
        lock (_lock)
        {
            if (!Persists)
            {
                return _inMemory;
            }

            try
            {
                if (!File.Exists(KeyPath))
                {
                    return null;
                }

                var key = Encoding.UTF8.GetString(unprotect!(File.ReadAllBytes(KeyPath)));
                return SteamGridDbKey.Normalize(key);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException or DecoderFallbackException)
            {
                Trace.TraceWarning($"SteamGridDB: gespeicherter Schlüssel nicht lesbar ({e.GetType().Name})");
                return null;
            }
        }
    }

    /// <summary>Keeps <paramref name="key"/>; null deletes it. False if it couldn't be written (the key then lasts until Arcadia closes).</summary>
    public bool Save(string? key)
    {
        lock (_lock)
        {
            _inMemory = key;
            if (!Persists)
            {
                return true;
            }

            try
            {
                if (key is null)
                {
                    File.Delete(KeyPath);
                    return true;
                }

                Directory.CreateDirectory(folder);
                var temporary = KeyPath + ".tmp";
                File.WriteAllBytes(temporary, protect!(Encoding.UTF8.GetBytes(key)));
                File.Move(temporary, KeyPath, overwrite: true);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
            {
                Trace.TraceWarning($"SteamGridDB: Schlüssel nicht gespeichert ({e.GetType().Name})");
                return false;
            }
        }
    }
}
