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
            ? id.Vendor.Equals("28DE", StringComparison.OrdinalIgnoreCase) && (!id.Product.Equals("11FF", StringComparison.OrdinalIgnoreCase) || HasValveName)
            : HasValveName;

    // SISR pairs a Steam virtual gamepad opened before its physical device with itself; then only the name tells.
    private bool HasValveName =>
        Name is { } name && (name.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) || name.Contains("Steam Deck", StringComparison.OrdinalIgnoreCase));

    /// <summary>Name and USB id for the log; the path itself carries an instance id that says nothing more.</summary>
    public string Description => UsbId() is { } id ? $"{Name} ({id.Vendor}:{id.Product})" : $"{Name} (ohne USB-ID)";

    /// <summary>
    /// The name for the settings. Behind Steam Input SDL sometimes gets Steam's untranslated controller type,
    /// e.g. <c>#controller_xbox360</c> (owner's PC, 2026-10-22, Steam Input on for every Xbox pad).
    /// </summary>
    public string DisplayName => Name switch
    {
        null or "" => "Unbekannter Controller",
        { } name when name.StartsWith(SteamTypePrefix, StringComparison.OrdinalIgnoreCase) => SteamTypeName(name[SteamTypePrefix.Length..]),
        { } name => name,
    };

    private const string SteamTypePrefix = "#controller_";

    private static string SteamTypeName(string type) => type.ToLowerInvariant() switch
    {
        "xbox360" => "Xbox 360 Controller",
        "xboxone" => "Xbox One Controller",
        "xboxelite" => "Xbox Elite Controller",
        "ps3" => "PlayStation 3 Controller",
        "ps4" => "PlayStation 4 Controller",
        "ps5" => "PlayStation 5 Controller",
        "switch_pro" => "Nintendo Switch Pro Controller",
        "generic" => "Generischer Controller",
        _ => $"Controller ({type.Replace('_', ' ')})",
    };

    private (string Vendor, string Product)? UsbId() =>
        Path is not null && UsbIdPattern().Match(Path) is { Success: true } match
            ? (match.Groups[1].Value.ToUpperInvariant(), match.Groups[2].Value.ToUpperInvariant())
            : null;

    [GeneratedRegex(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UsbIdPattern();
}

/// <summary>
/// One entry of SISR's <c>/devices</c>: the physical gamepad, Steam's virtual gamepad for it while Steam Input
/// takes it, and, while SISR emulates it, the VIIPER device.
/// </summary>
public sealed record SisrDevice(
    [property: JsonPropertyName("real_device")] SisrGamepad? RealDevice,
    [property: JsonPropertyName("viiper_device")] System.Text.Json.JsonElement? ViiperDevice,
    [property: JsonPropertyName("steam_virtual_device")] SisrGamepad? SteamVirtualDevice = null)
{
    public bool IsEmulated => ViiperDevice is not null;

    /// <summary>
    /// Steam Input hands this device to SISR. SISR creates its VIIPER device only on the device's first input
    /// (SISR 0.6.2, <c>event/handler/gamepadupdated</c>), so this tells it before anyone touches the pad (#143).
    /// </summary>
    public bool IsUnderSteamInput => SteamVirtualDevice is not null;

    /// <summary>
    /// SISR emulates every controller Steam Input hands it, not only the Steam Controller (#124). Without
    /// a physical device to look at it counts as one, as before.
    /// </summary>
    public bool IsSteamController => RealDevice?.IsValveController ?? true;
}

/// <summary>
/// The Steam Controllers SISR emulates, and any other controller Steam Input gave SISR, emulated yet or not
/// (<see cref="SisrDevice.IsUnderSteamInput"/>).
/// </summary>
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
    /// The Steam Controllers SISR currently emulates and the other controllers Steam Input hands it, or null
    /// if SISR doesn't answer.
    /// </summary>
    public async Task<SisrControllers?> GetEmulatedControllersAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsReachableAsync(cancellationToken))
        {
            return null;
        }

        var devices = (await GetAsync<List<SisrDevice?>>("devices", cancellationToken) ?? []).OfType<SisrDevice>().ToList();
        return new SisrControllers(
            devices.Count(d => d.IsEmulated && d.IsSteamController),
            devices.Where(d => !d.IsSteamController && (d.IsEmulated || d.IsUnderSteamInput)).Select(d => d.RealDevice!).ToList());
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
