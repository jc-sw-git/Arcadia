using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Arcadia.ControllerSupport;

public sealed record SisrSteamStatus(
    [property: JsonPropertyName("steam_running")] bool SteamRunning,
    [property: JsonPropertyName("cef_debug_enabled")] bool CefDebugEnabled,
    [property: JsonPropertyName("cef_debug_reachable")] bool CefDebugReachable);

public sealed record SisrViiperStatus(
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("status")] SisrViiperServer? Status);

public sealed record SisrViiperServer(
    [property: JsonPropertyName("server")] string? Server,
    [property: JsonPropertyName("version")] string? Version);

/// <summary>A gamepad as SDL sees it inside SISR.</summary>
public sealed partial record SisrGamepad(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("path")] string? Path)
{
    /// <summary>
    /// Valve's USB vendor id in the device path, except Steam's own virtual gamepad (28DE:11FF), which
    /// stands for whatever controller Steam Input passes on. SDL's HID path names the device, e.g.
    /// <c>\\?\HID#VID_28DE&amp;PID_1304&amp;MI_02#…</c>; its XInput path names no vendor.
    /// </summary>
    public bool IsValveController =>
        UsbId() is { } id
            ? id.Vendor.Equals("28DE", StringComparison.OrdinalIgnoreCase) && !id.Product.Equals("11FF", StringComparison.OrdinalIgnoreCase)
            : Name is { } name && (name.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) || name.Contains("Steam Deck", StringComparison.OrdinalIgnoreCase));

    /// <summary>Name and USB id for the log; the path itself carries an instance id that says nothing more.</summary>
    public string Description => UsbId() is { } id ? $"{Name} ({id.Vendor}:{id.Product})" : $"{Name} (ohne USB-ID)";

    private (string Vendor, string Product)? UsbId() =>
        Path is not null && UsbIdPattern().Match(Path) is { Success: true } match
            ? (match.Groups[1].Value.ToUpperInvariant(), match.Groups[2].Value.ToUpperInvariant())
            : null;

    [GeneratedRegex(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UsbIdPattern();
}

/// <summary>One entry of SISR's <c>/devices</c>: the physical gamepad and, while SISR emulates it, the VIIPER device.</summary>
public sealed record SisrDevice(
    [property: JsonPropertyName("real_device")] SisrGamepad? RealDevice,
    [property: JsonPropertyName("viiper_device")] System.Text.Json.JsonElement? ViiperDevice)
{
    public bool IsEmulated => ViiperDevice is not null;

    /// <summary>
    /// SISR emulates every controller Steam Input hands it, not only the Steam Controller (#124). Without
    /// a physical device to look at it counts as one, as before.
    /// </summary>
    public bool IsSteamController => RealDevice?.IsValveController ?? true;
}

/// <summary>The controllers SISR emulates: Steam Controllers, and any other controller Steam Input gave it.</summary>
public sealed record SisrControllers(int SteamControllers, IReadOnlyList<SisrGamepad> Others)
{
    public int Total => SteamControllers + Others.Count;
}

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
    /// The controllers SISR currently emulates (entries with a VIIPER device), or null if SISR
    /// doesn't answer.
    /// </summary>
    public async Task<SisrControllers?> GetEmulatedControllersAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsReachableAsync(cancellationToken))
        {
            return null;
        }

        var emulated = (await GetAsync<List<SisrDevice?>>("devices", cancellationToken) ?? [])
            .OfType<SisrDevice>().Where(d => d.IsEmulated).ToList();
        return new SisrControllers(
            emulated.Count(d => d.IsSteamController),
            emulated.Where(d => !d.IsSteamController).Select(d => d.RealDevice!).ToList());
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
