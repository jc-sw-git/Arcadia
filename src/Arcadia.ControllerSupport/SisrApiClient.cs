using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Arcadia.ControllerSupport;

public sealed record SisrSteamStatus(
    [property: JsonPropertyName("steam_running")] bool SteamRunning,
    [property: JsonPropertyName("cef_debug_enabled")] bool CefDebugEnabled,
    [property: JsonPropertyName("cef_debug_reachable")] bool CefDebugReachable,
    [property: JsonPropertyName("marker_shortcut_present")] bool MarkerShortcutPresent);

public sealed record SisrViiperStatus(
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("status")] SisrViiperServer? Status);

public sealed record SisrViiperServer(
    [property: JsonPropertyName("server")] string? Server,
    [property: JsonPropertyName("version")] string? Version);

public sealed record SisrDevice(
    [property: JsonPropertyName("viiper_device")] System.Text.Json.JsonElement? ViiperDevice);

public sealed record SisrVersionInfo(
    [property: JsonPropertyName("version")] string Version);

/// <summary>
/// SISR's local HTTP API (see SISR's openapi.yaml). Arcadia starts SISR with a fixed port so it
/// can find it.
/// </summary>
public sealed class SisrApiClient(HttpClient http, int port)
{
    private readonly Uri _base = new($"http://127.0.0.1:{port}/api/v1/");

    public Task<SisrSteamStatus?> GetSteamStatusAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SisrSteamStatus>("steam/status", cancellationToken);

    public Task<SisrViiperStatus?> PingViiperAsync(CancellationToken cancellationToken = default) =>
        PostAsync<SisrViiperStatus>("viiper/ping", cancellationToken);

    public Task<SisrVersionInfo?> GetVersionAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SisrVersionInfo>("version/info", cancellationToken);

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default) =>
        await GetVersionAsync(cancellationToken) is not null;

    /// <summary>
    /// How many controllers SISR currently emulates (entries with a VIIPER device), or null if
    /// SISR doesn't answer.
    /// </summary>
    public async Task<int?> GetEmulatedControllerCountAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsReachableAsync(cancellationToken))
        {
            return null;
        }

        var devices = await GetAsync<List<SisrDevice>>("devices", cancellationToken);
        return devices?.Count(d => d.ViiperDevice is not null) ?? 0;
    }

    public Task<bool> QuitAsync(CancellationToken cancellationToken = default) => SendAsync("quit", cancellationToken);

    /// <summary>Tells SISR whether to keep forcing Steam to use its marker's controller layout.</summary>
    public async Task<bool> SetForceConfigAsync(bool enforce, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await http.PostAsJsonAsync(new Uri(_base, "steam/force-config"), new { enforce }, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public Task<bool> CreateMarkerShortcutAsync(CancellationToken cancellationToken = default) =>
        SendAsync("steam/cef/create-marker-shortcut", cancellationToken);

    public Task<bool> EnableCefRemoteDebuggingAsync(CancellationToken cancellationToken = default) =>
        SendAsync("steam/enable-cef-remote-debugging", cancellationToken);

    public Task<bool> RestartSteamAsync(CancellationToken cancellationToken = default) =>
        SendAsync("steam/restart", cancellationToken);

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            return await http.GetFromJsonAsync<T>(new Uri(_base, path), timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private async Task<T?> PostAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await http.PostAsync(new Uri(_base, path), null, timeout.Token);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(timeout.Token) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private async Task<bool> SendAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await http.PostAsync(new Uri(_base, path), null, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
