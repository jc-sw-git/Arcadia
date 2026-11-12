using Arcadia.Core.Metadata;

namespace Arcadia.App.Services;

/// <summary>Where the App keeps the metadata cache (#493): <c>%LOCALAPPDATA%\Arcadia\metadata</c>, which may be thrown away.</summary>
public static class MetadataFolder
{
    public static string Default { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcadia", "metadata");

    /// <summary>
    /// The cache with a client of its own: it sends no cookies (the shared client holds the Steam sign-in's), no account
    /// data and follows no redirect to another host; a store page gets only its address, which holds the game's id.
    /// </summary>
    public static MetadataService CreateService(TimeProvider time)
    {
        var http = new HttpClient(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false, ConnectTimeout = MetadataService.RequestTimeout })
        {
            // The service ends every request after 10 s itself, on its own clock.
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return new MetadataService(new MetadataCache(Default), http, time) { Log = AppLog.Write };
    }
}
