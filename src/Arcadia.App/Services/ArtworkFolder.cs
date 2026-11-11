using Arcadia.Core.Artwork;

namespace Arcadia.App.Services;

/// <summary>Where the App keeps the artwork cache (#13): <c>%LOCALAPPDATA%\Arcadia\artwork</c>, which may be thrown away.</summary>
public static class ArtworkFolder
{
    public static string Default { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcadia", "artwork");

    /// <summary>
    /// The cache with a client of its own: it sends no cookies (the shared client holds the Steam sign-in's), no key and no
    /// account data, only the image's address (#487).
    /// </summary>
    public static ArtworkService CreateService(TimeProvider time) =>
        new(new ArtworkCache(Default), new HttpClient(new SocketsHttpHandler { UseCookies = false, ConnectTimeout = ArtworkService.DownloadTimeout }), time)
        {
            Log = AppLog.Write,
        };
}
