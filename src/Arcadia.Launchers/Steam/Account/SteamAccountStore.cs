using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Arcadia.Core.Diagnostics;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>The stored sign-in: the account, its name and the last web API token. Never logged.</summary>
/// <param name="AccessToken">The store page's web API token as read last; it lives about a day.</param>
/// <param name="Expired">The store page was no longer signed in: signed out, with a status message until a new sign-in.</param>
/// <param name="RefreshToken">
/// Only in a sign-in of the first #83 build, which posed as Steam's phone app; such a sign-in is deleted, not used.
/// </param>
internal sealed record SteamSignIn(string? AccessToken, ulong SteamId, string Name, bool Expired = false, string? RefreshToken = null);

/// <summary>The owned games as fetched last, for a start offline or before the next fetch.</summary>
/// <param name="Packages">The account's packages from the store; null in a list cached before they were kept.</param>
internal sealed record SteamOwnedList(DateTimeOffset ListedAt, IReadOnlyList<SteamOwnedGame> Games, IReadOnlyList<uint>? Packages = null);

/// <summary>
/// Keeps the Steam sign-in (#83) in <c>steam-account.dat</c>, encrypted for the current Windows user (DPAPI), the
/// owned games in <c>steam-owned-games.json</c> and the profile's avatar in <c>steam-avatar.jpg</c>, all in Arcadia's
/// data folder. Without DPAPI (not Windows) the sign-in
/// lasts only until Arcadia closes. A file that can't be read counts as missing; reading and writing never throw.
/// </summary>
/// <param name="protect">Encrypts the sign-in; null keeps it in memory only.</param>
internal sealed class SteamAccountStore(string folder, Func<byte[], byte[]>? protect, Func<byte[], byte[]>? unprotect)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _lock = new();
    private SteamSignIn? _inMemory;

    public static SteamAccountStore CreateDefault(string? folder = null) => OperatingSystem.IsWindows()
        ? new(folder ?? LogLocation.DataFolder, Protect, Unprotect)
        : new(folder ?? LogLocation.DataFolder, null, null);

    private string SignInPath => Path.Combine(folder, "steam-account.dat");

    private string OwnedPath => Path.Combine(folder, "steam-owned-games.json");

    private string AvatarPath => Path.Combine(folder, "steam-avatar.jpg");

    /// <summary>The stored avatar's file, null if there is none.</summary>
    public string? AvatarFile
    {
        get
        {
            lock (_lock)
            {
                return File.Exists(AvatarPath) ? AvatarPath : null;
            }
        }
    }

    /// <summary>Keeps <paramref name="picture"/> as the account's avatar; null deletes it. Returns the file, if one is kept.</summary>
    public string? SaveAvatar(byte[]? picture)
    {
        lock (_lock)
        {
            if (picture is null)
            {
                Delete(AvatarPath);
                return null;
            }

            Write(AvatarPath, picture);
            return File.Exists(AvatarPath) ? AvatarPath : null;
        }
    }

    public SteamSignIn? LoadSignIn()
    {
        lock (_lock)
        {
            if (protect is null || unprotect is null)
            {
                return _inMemory;
            }

            try
            {
                return File.Exists(SignInPath) ? JsonSerializer.Deserialize<SteamSignIn>(unprotect(File.ReadAllBytes(SignInPath)), Json) : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
            {
                Trace.TraceWarning($"Steam-Konto: gespeicherte Anmeldung nicht lesbar ({e.GetType().Name})");
                return null;
            }
        }
    }

    public void SaveSignIn(SteamSignIn signIn)
    {
        lock (_lock)
        {
            if (protect is null)
            {
                _inMemory = signIn;
                return;
            }

            Write(SignInPath, protect(JsonSerializer.SerializeToUtf8Bytes(signIn, Json)));
        }
    }

    public SteamOwnedList? LoadOwned()
    {
        lock (_lock)
        {
            try
            {
                return File.Exists(OwnedPath) ? JsonSerializer.Deserialize<SteamOwnedList>(File.ReadAllBytes(OwnedPath), Json) : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Trace.TraceWarning($"Steam-Konto: Liste der Spiele nicht lesbar ({e.GetType().Name})");
                return null;
            }
        }
    }

    public void SaveOwned(SteamOwnedList owned)
    {
        lock (_lock)
        {
            Write(OwnedPath, JsonSerializer.SerializeToUtf8Bytes(owned, Json));
        }
    }

    /// <summary>Forgets the owned games; <paramref name="signIn"/> replaces the sign-in, or null deletes it.</summary>
    public void Clear(SteamSignIn? signIn)
    {
        lock (_lock)
        {
            Delete(OwnedPath);
            if (signIn is null)
            {
                _inMemory = null;
                Delete(SignInPath);
                Delete(AvatarPath);
            }
            else
            {
                SaveSignIn(signIn);
            }
        }
    }

    // Written to a temporary file first, so a crash mid-write leaves the old file.
    private static void Write(string path, byte[] content)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Steam-Konto: {Path.GetFileName(path)} nicht gespeichert ({e.Message})");
        }
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Steam-Konto: {Path.GetFileName(path)} nicht gelöscht ({e.Message})");
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[] Protect(byte[] data) => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
