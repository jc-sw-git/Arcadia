using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Arcadia.Core.Settings;

/// <summary>
/// One secret of the player (the SteamGridDB key #489, the IGDB access #494) in a file of Arcadia's data folder, encrypted
/// by <c>protect</c> (DPAPI for the current Windows user, handed in by the App). Without it (not Windows) the secret lasts
/// only until Arcadia closes, like the Steam sign-in, and nothing about it is written. A file that can't be read or
/// decrypted counts as no secret; reading and writing never throw, and no log line holds the secret.
/// </summary>
/// <param name="what">The service the secret is for, in a log line, e.g. "SteamGridDB"; never the secret.</param>
/// <param name="protect">Encrypts the secret; null keeps it in memory only.</param>
public sealed class SecretFile(string folder, string fileName, string what, Func<byte[], byte[]>? protect, Func<byte[], byte[]>? unprotect)
{
    private readonly Lock _lock = new();
    private string? _inMemory;

    public string Path => System.IO.Path.Combine(folder, fileName);

    /// <summary>The secret is written to disk (encrypted) and lasts across a restart.</summary>
    public bool Persists => protect is not null && unprotect is not null;

    /// <summary>The secret as it was saved; null without one, or when the file can't be read or decrypted.</summary>
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
                return File.Exists(Path) ? Encoding.UTF8.GetString(unprotect!(File.ReadAllBytes(Path))) : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException or DecoderFallbackException)
            {
                Trace.TraceWarning($"{what}: gespeicherter Wert nicht lesbar ({e.GetType().Name})");
                return null;
            }
        }
    }

    /// <summary>Keeps <paramref name="secret"/>; null deletes it. False if it couldn't be written (it then lasts until Arcadia closes).</summary>
    public bool Save(string? secret)
    {
        lock (_lock)
        {
            _inMemory = secret;
            if (!Persists)
            {
                return true;
            }

            try
            {
                if (secret is null)
                {
                    File.Delete(Path);
                    return true;
                }

                Directory.CreateDirectory(folder);
                var temporary = Path + ".tmp";
                File.WriteAllBytes(temporary, protect!(Encoding.UTF8.GetBytes(secret)));
                File.Move(temporary, Path, overwrite: true);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
            {
                Trace.TraceWarning($"{what}: nicht gespeichert ({e.GetType().Name})");
                return false;
            }
        }
    }
}
