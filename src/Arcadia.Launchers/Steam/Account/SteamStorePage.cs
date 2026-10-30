using System.Globalization;
using System.Text.Json;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>
/// Steam's store web page as Arcadia's browser shows and reads it (#83), the way comparable products sign in to Steam: the user
/// signs in on the store's own login page (with its QR code, or a password Arcadia never sees); afterwards every store
/// page holds the account (<c>data-userinfo</c>) and a web API token (<c>data-store_user_config</c>, <c>webapi_token</c>)
/// in the element <c>#application_config</c>. That token reads the owned games over the web API.
/// </summary>
internal static class SteamStorePage
{
    public static readonly Uri LoginPage = new("https://store.steampowered.com/login/?redir=explore%2F");

    /// <summary>A light store page that holds the account and token once signed in (comparable products read the same).</summary>
    public static readonly Uri StorePage = new("https://store.steampowered.com/explore/");

    /// <summary>The store's own list of what the account owns: apps (incl. soundtracks, tools, DLC) and packages.</summary>
    public static readonly Uri UserDataPage = new("https://store.steampowered.com/dynamicstore/userdata/");

    /// <summary>Where Steam keeps the sign-in's cookies; signing out deletes them.</summary>
    public static readonly IReadOnlyList<string> CookieDomains = ["steampowered.com", "steamcommunity.com"];

    /// <summary>Returns the two attributes of <c>#application_config</c> as JSON, or an empty string on another page.</summary>
    public const string AccountScript = """
        (() => {
          const config = document.getElementById('application_config');
          return config ? JSON.stringify({ user: config.getAttribute('data-userinfo'), config: config.getAttribute('data-store_user_config') }) : '';
        })()
        """;

    /// <summary>The page's text: the user data page is plain JSON shown as text.</summary>
    public const string TextScript = "document.body ? document.body.innerText : ''";

    /// <summary>The account and its token if the page is signed in; null on any other page or answer.</summary>
    public static SteamStoreSignIn? SignInOf(string scriptResult)
    {
        try
        {
            using var page = JsonDocument.Parse(scriptResult);
            if (page.RootElement.ValueKind != JsonValueKind.Object
                || Attribute(page.RootElement, "user") is not { } userJson
                || Attribute(page.RootElement, "config") is not { } configJson)
            {
                return null;
            }

            using var user = JsonDocument.Parse(userJson);
            using var config = JsonDocument.Parse(configJson);
            var signedIn = user.RootElement.ValueKind == JsonValueKind.Object
                && user.RootElement.TryGetProperty("logged_in", out var loggedIn) && loggedIn.ValueKind == JsonValueKind.True;
            if (!signedIn
                || !user.RootElement.TryGetProperty("steamid", out var id)
                || !ulong.TryParse(id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText(), NumberStyles.None, CultureInfo.InvariantCulture, out var steamId)
                || config.RootElement.ValueKind != JsonValueKind.Object
                || !config.RootElement.TryGetProperty("webapi_token", out var token) || token.GetString() is not { Length: > 0 } webApiToken)
            {
                return null;
            }

            return new SteamStoreSignIn(steamId, webApiToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>What the user data page lists; null if it isn't that JSON (signed out, an error page).</summary>
    public static SteamStoreOwnership? OwnershipOf(string pageText)
    {
        try
        {
            using var json = JsonDocument.Parse(pageText);
            var apps = Ids(json.RootElement, "rgOwnedApps");
            return apps.Count > 0 ? new SteamStoreOwnership(apps, Ids(json.RootElement, "rgOwnedPackages")) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Attribute(JsonElement page, string name) =>
        page.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<uint> Ids(JsonElement root, string property)
    {
        var ids = new List<uint>();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetUInt32(out var id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }
}

/// <summary>The account a store page is signed in with, and its web API token (never logged).</summary>
internal sealed record SteamStoreSignIn(ulong SteamId, string WebApiToken);
