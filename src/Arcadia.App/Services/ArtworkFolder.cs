using System.Runtime.Versioning;
using System.Security.Cryptography;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;

namespace Arcadia.App.Services;

/// <summary>Where the App keeps the artwork cache (#13): <c>%LOCALAPPDATA%\Arcadia\artwork</c>, which may be thrown away.</summary>
public static class ArtworkFolder
{
    public static string Default { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcadia", "artwork");

    /// <summary>
    /// The cache with a client of its own: it sends no cookies (the shared client holds the Steam sign-in's) and no account
    /// data. Image downloads get only the image's address; SteamGridDB's API alone gets the key, in each request's
    /// header (#487, #489). The key is kept in <paramref name="dataFolder"/>, encrypted for the Windows user, elsewhere only
    /// until Arcadia closes.
    /// </summary>
    public static ArtworkService CreateService(TimeProvider time, SettingsService settings, string dataFolder)
    {
        var http = new HttpClient(new SocketsHttpHandler { UseCookies = false, ConnectTimeout = ArtworkService.DownloadTimeout });
        var store = OperatingSystem.IsWindows() ? new SteamGridDbKeyStore(dataFolder, Protect, Unprotect) : new SteamGridDbKeyStore(dataFolder, null, null);
        return new(new ArtworkCache(Default), http, time)
        {
            Log = AppLog.Write,
            Settings = () => settings.Current.Artwork,
            SteamGridDb = new SteamGridDbKey(store, new SteamGridDbClient(http, time) { Log = AppLog.Write }, time),
        };
    }

    [SupportedOSPlatform("windows")]
    private static byte[] Protect(byte[] data) => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
