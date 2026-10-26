using System.Diagnostics;
using System.Text;
using Viiper.Client.Devices.Keyboard;

namespace SteamInputProbe;

/// <summary>
/// Spike #118: can a UAC consent prompt (Secure Desktop) be answered from a plain, non-elevated process with a
/// VIIPER virtual keyboard and mouse? The probe keeps a keyboard/mouse ready, watches which desktop has the input,
/// tells a UAC prompt apart from the lock/sign-in screen, and — when a one-shot command file is armed — plays a
/// key/mouse sequence into the prompt. It reads the controller with SDL3 the whole time, to show Arcadia could keep
/// reading the pad while the prompt is up. It logs to a file only; the step script (Run-Checks-118.ps1) drives it.
/// </summary>
internal sealed class UacProbe(ProbeOptions options, ProbeLog log) : IAsyncDisposable
{
    private readonly string _armDir = Path.GetDirectoryName(log.Path)!;
    private ViiperHid? _hid;
    private SdlReader? _sdl;
    private string _state = "";

    public async Task<int> RunAsync(CancellationToken cancel)
    {
        _hid = await ViiperHid.StartAsync(log, cancel);
        if (_hid is null)
        {
            log.Line("UAC probe: no VIIPER keyboard/mouse — cannot continue");
            return 2;
        }
        _sdl = new SdlReader(log);
        log.Line($"UAC probe ready. Arm a sequence by writing '{Path.Combine(_armDir, "armed.txt")}', stop with 'stop'. "
                 + $"Poll {options.PollMs} ms.");

        try
        {
            while (!cancel.IsCancellationRequested && !options.ShouldStop(log))
            {
                if (File.Exists(Path.Combine(_armDir, "stop"))) { log.Line("UAC probe: stop file seen"); break; }

                // armed-now.txt: play at once, on whatever desktop — step 1 checks the devices on the normal desktop.
                if (File.Exists(Path.Combine(_armDir, "armed-now.txt")))
                    await PlayFileAsync("armed-now.txt", "now (current desktop)", requireUac: false, cancel);

                var (cls, detail) = Classify();
                if (cls != _state)
                {
                    log.Line($"DESKTOP: {cls}{(detail is null ? "" : " — " + detail)}");
                    _state = cls;
                    // armed.txt: play only when a real UAC prompt appears (one-shot).
                    if (cls == "uac") await PlayFileAsync("armed.txt", detail ?? "UAC", requireUac: true, cancel);
                }
                await Task.Delay(options.PollMs, cancel);
            }
        }
        catch (OperationCanceledException) { }
        return 0;
    }

    /// <summary>Reads a one-shot command file and plays its sequence (line 1 = tokens, line 2 = gap ms).</summary>
    private async Task PlayFileAsync(string fileName, string label, bool requireUac, CancellationToken cancel)
    {
        var path = Path.Combine(_armDir, fileName);
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
            File.Delete(path); // one-shot: disarm at once, so the next prompt is not answered by accident
        }
        catch (Exception ex) { log.Line($"UAC probe: cannot read {fileName}: {ex.Message}"); return; }

        var tokens = lines.Length > 0 ? lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
        var gapMs = lines.Length > 1 && int.TryParse(lines[1].Trim(), out var g) ? g : 1000;
        log.Line($"UAC probe: playing [{string.Join(' ', tokens)}] gap {gapMs} ms into {label}");

        var clock = Stopwatch.StartNew();
        foreach (var token in tokens)
        {
            if (cancel.IsCancellationRequested) break;
            // For the UAC sequence: if the prompt closed mid-way, stop so no key leaks onto whatever came next.
            if (requireUac && Classify().Cls != "uac") { log.Line($"UAC probe: prompt gone after {clock.ElapsedMilliseconds} ms — stopping sequence"); break; }
            await PlayTokenAsync(token, clock.ElapsedMilliseconds, cancel);
            await Task.Delay(gapMs, cancel);
        }
        log.Line($"UAC probe: sequence done after {clock.ElapsedMilliseconds} ms");
    }

    private async Task PlayTokenAsync(string token, long atMs, CancellationToken cancel)
    {
        if (token == "click") { log.Line($"{atMs,6} ms into prompt: mouse LEFT click"); await _hid!.ClickAsync(1, cancel); return; }
        if (token == "rclick") { log.Line($"{atMs,6} ms into prompt: mouse RIGHT click"); await _hid!.ClickAsync(2, cancel); return; }
        if (token.StartsWith("m") && token[1..].Split(',') is [var sx, var sy]
            && short.TryParse(sx, out var dx) && short.TryParse(sy, out var dy))
        {
            log.Line($"{atMs,6} ms into prompt: mouse move ({dx},{dy})");
            await _hid!.MoveMouseAsync(dx, dy, cancel);
            return;
        }
        if (KeyFor(token) is { } key)
        {
            log.Line($"{atMs,6} ms into prompt: key {token} (HID 0x{(byte)key:X2})");
            await _hid!.TapKeyAsync(key, cancel);
            return;
        }
        log.Line($"{atMs,6} ms into prompt: unknown token '{token}' — skipped");
    }

    private static Key? KeyFor(string token)
    {
        var t = token.ToLowerInvariant();
        var named = t switch
        {
            "tab" => Key.Tab, "enter" => Key.Enter, "esc" => Key.Escape, "space" => Key.Space,
            "up" => Key.Up, "down" => Key.Down, "left" => Key.Left, "right" => Key.Right,
            "backspace" => Key.Backspace, "home" => Key.Home, "end" => Key.End,
            _ => (Key?)null,
        };
        if (named is not null) return named;
        if (t.Length == 1 && t[0] is >= 'a' and <= 'z') return (Key)((byte)Key.A + (t[0] - 'a')); // letter → HID usage
        return null;
    }

    /// <summary>Which desktop has the input, and — when it is a Secure Desktop — whether it is UAC or lock/sign-in.</summary>
    private static (string Cls, string? Detail) Classify()
    {
        var secure = !TryReadInputDesktop(out var name);
        if (!secure) return ("interactive", $"input desktop '{name}'");

        var consent = Process.GetProcessesByName("consent");
        var logon = Process.GetProcessesByName("LogonUI");
        try
        {
            if (consent.Length > 0)
            {
                var since = DateTime.Now - consent[0].StartTime;
                return ("uac", $"consent.exe pid {consent[0].Id}, running {since.TotalMilliseconds:0} ms");
            }
            if (logon.Length > 0) return ("lock/signin", $"LogonUI.exe pid {logon[0].Id}, no consent.exe");
            return ("secure (unknown)", name is null ? "input desktop not accessible" : $"input desktop '{name}'");
        }
        finally
        {
            foreach (var p in consent) p.Dispose();
            foreach (var p in logon) p.Dispose();
        }
    }

    private static bool TryReadInputDesktop(out string? name)
    {
        name = null;
        var desktop = Native.OpenInputDesktop(0, false, Native.DesktopReadObjects);
        if (desktop == IntPtr.Zero) return false; // denied → a Secure Desktop is in front
        try
        {
            var buffer = new byte[256];
            if (Native.GetUserObjectInformationW(desktop, Native.UoiName, buffer, (uint)buffer.Length, out var needed))
                name = Encoding.Unicode.GetString(buffer, 0, (int)Math.Max(0, needed - 2)); // strip the trailing NUL
            return name == "Default"; // only the ordinary interactive desktop counts as "not secure"
        }
        finally { Native.CloseDesktop(desktop); }
    }

    public async ValueTask DisposeAsync()
    {
        _sdl?.Dispose();
        if (_hid is not null) await _hid.DisposeAsync();
    }
}
