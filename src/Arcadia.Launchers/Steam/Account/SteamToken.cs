using System.Globalization;
using System.Text.Json;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>
/// What Arcadia reads from a Steam token (a JWT): the account it belongs to (<c>sub</c>, the SteamID64) and when it
/// expires (<c>exp</c>). The signature isn't checked: the token came straight from Steam over HTTPS, and Steam checks it
/// on every use.
/// </summary>
internal static class SteamToken
{
    public static ulong? SteamIdOf(string token) =>
        Claims(token) is { } claims && claims.TryGetProperty("sub", out var sub)
        && ulong.TryParse(sub.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    public static DateTimeOffset? ExpiresAt(string token) =>
        Claims(token) is { } claims && claims.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    private static JsonElement? Claims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
            return json.RootElement.ValueKind == JsonValueKind.Object ? json.RootElement.Clone() : null;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }
}
