using System.Diagnostics;
using System.Text.Json;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>
/// The games the account hid in Steam's library ("Versteckt"), which Steam's "Alle" doesn't count (#83). Steam keeps its
/// collections in <c>userdata/&lt;id&gt;/config/cloudstorage/cloud-storage-namespace-1.json</c>: a list of
/// <c>[key, {key, value, …}]</c>, where the entry <c>user-collections.hidden</c> holds the collection as a JSON string
/// with the app ids in <c>added</c> (owner's PC, 2026-10-27).
/// </summary>
internal static class SteamHiddenGames
{
    public static IReadOnlySet<uint> Read(string? userDataDirectory)
    {
        if (userDataDirectory is null)
        {
            return new HashSet<uint>();
        }

        var path = Path.Combine(userDataDirectory, "config", "cloudstorage", "cloud-storage-namespace-1.json");
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : new HashSet<uint>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Steam: versteckte Spiele nicht lesbar ({e.GetType().Name})");
            return new HashSet<uint>();
        }
    }

    public static IReadOnlySet<uint> Parse(string json)
    {
        var hidden = new HashSet<uint>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return hidden;
            }

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Array && entry.GetArrayLength() == 2
                    && entry[0].GetString() == "user-collections.hidden" && entry[1].ValueKind == JsonValueKind.Object
                    && !(entry[1].TryGetProperty("is_deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True)
                    && entry[1].TryGetProperty("value", out var value) && value.GetString() is { } collection)
                {
                    using var ids = JsonDocument.Parse(collection);
                    if (ids.RootElement.TryGetProperty("added", out var added) && added.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var id in added.EnumerateArray())
                        {
                            if (id.TryGetUInt32(out var appId))
                            {
                                hidden.Add(appId);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            Trace.TraceWarning($"Steam: versteckte Spiele nicht lesbar ({e.GetType().Name})");
        }

        return hidden;
    }
}
