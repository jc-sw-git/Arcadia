using System.Runtime.Versioning;
using System.Security.Cryptography;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;

namespace Arcadia.App.Services;

/// <summary>Where the App keeps the metadata cache (#493): <c>%LOCALAPPDATA%\Arcadia\metadata</c>, which may be thrown away.</summary>
public static class MetadataFolder
{
    public static string Default { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcadia", "metadata");

    /// <summary>
    /// The cache with a client of its own: it sends no cookies (the shared client holds the Steam sign-in's), no account
    /// data and follows no redirect to another host; a store page gets only its address, which holds the game's id. IGDB's
    /// access (#494) is kept in <paramref name="dataFolder"/>, encrypted for the Windows user, elsewhere only until Arcadia
    /// closes; the order of the sources and "Bei mehreren Treffern" come from the settings.
    /// </summary>
    public static MetadataService CreateService(TimeProvider time, SettingsService settings, string dataFolder)
    {
        var http = new HttpClient(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false, ConnectTimeout = MetadataService.RequestTimeout })
        {
            // The service ends every request after 10 s itself, on its own clock.
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var store = OperatingSystem.IsWindows() ? new IgdbAccessStore(dataFolder, Protect, Unprotect) : new IgdbAccessStore(dataFolder, null, null);
        return new MetadataService(new MetadataCache(Default), http, time)
        {
            Log = AppLog.Write,
            Language = UiLanguage.Culture,
            FirstAgeRatingBoard = AgeRatingRegion.System,
            Settings = () => settings.Current.Metadata,
            SeveralHits = () => settings.Current.Metadata.SeveralHitsOr(settings.Current.Artwork.WithValidValues().SeveralHits),
            Igdb = new IgdbAccess(store, new IgdbClient(http, time) { Log = AppLog.Write }, time),
        };
    }

    [SupportedOSPlatform("windows")]
    private static byte[] Protect(byte[] data) => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
