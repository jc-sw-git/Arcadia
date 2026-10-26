namespace SteamInputProbe;

/// <summary>
/// Reads the physical controllers with SDL3 directly, beside a running Steam: does SDL's driver for the new Steam
/// Controller deliver buttons, trackpads and gyro while Steam owns the device, and does Steam keep working?
/// </summary>
internal sealed class RawProbe(ProbeOptions options, ProbeLog log)
{
    private sealed class Pad(IntPtr handle, string name, bool isValve)
    {
        public IntPtr Handle { get; } = handle;
        public string Name { get; } = name;
        public bool IsValve { get; } = isValve;
        public int Buttons;
        public int Axes;
        public readonly int[] Touch = new int[2];
        public int Gyro;
        public int Accel;
        public float MaxGyro;
        public (float X, float Y, float Z) LastGyro;
    }

    private readonly Dictionary<uint, Pad> _pads = [];

    public int Run(CancellationToken cancel)
    {
        var version = Sdl.SDL_GetVersion();
        log.Line($"SDL {version / 1000000}.{version / 1000 % 1000}.{version % 1000}");
        // As SISR sets them (cli/cmd/sisr/hints.go), so SDL reads the controller with its own HID driver even
        // while the probe isn't in front.
        Sdl.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
        Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI", "1");
        Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI_STEAM", "1");
        Sdl.SDL_SetHint("SDL_JOYSTICK_ENHANCED_REPORTS", "1");
        Sdl.SDL_SetHint("SDL_JOYSTICK_WGI", "0");
        Sdl.SDL_SetHint("SDL_JOYSTICK_GAMEINPUT", "0");
        if (!Sdl.SDL_Init(Sdl.InitGamepad | Sdl.InitSensor))
        {
            log.Line($"SDL_Init FAILED: {Sdl.Text(Sdl.SDL_GetError())}");
            return 2;
        }

        using var watch = new DesktopWatch(log);
        var nextPrompt = 0;
        var prompts = options.Prompts.OrderBy(p => p.At).ToList();
        var nextReport = 1000L;
        try
        {
            while (!cancel.IsCancellationRequested && !options.ShouldStop(log))
            {
                while (nextPrompt < prompts.Count && log.ElapsedMs >= prompts[nextPrompt].At * 1000)
                    log.Line($"PROMPT >>> {prompts[nextPrompt++].Text}");
                if (Sdl.SDL_WaitEventTimeout(out var e, 50)) Handle(e);
                if (log.ElapsedMs >= nextReport)
                {
                    Report();
                    nextReport += 1000;
                }
            }
        }
        finally
        {
            foreach (var pad in _pads.Values) Sdl.SDL_CloseGamepad(pad.Handle);
            Sdl.SDL_Quit();
            log.Line("SDL closed");
        }
        return 0;
    }

    private void Handle(SdlEvent e)
    {
        switch (e.Type)
        {
            case Sdl.EventGamepadAdded:
                Open(e.Which);
                break;
            case Sdl.EventGamepadRemoved when _pads.Remove(e.Which, out var gone):
                log.Line($"pad {e.Which} removed ({gone.Name})");
                Sdl.SDL_CloseGamepad(gone.Handle);
                break;
            case Sdl.EventGamepadButtonDown or Sdl.EventGamepadButtonUp when _pads.TryGetValue(e.Which, out var pad):
                pad.Buttons++;
                var button = Sdl.Text(Sdl.SDL_GetGamepadStringForButton(e.ButtonOrAxis));
                log.Line($"pad {e.Which}: button {(button.Length > 0 ? button : "#" + e.ButtonOrAxis)} {(e.Down != 0 ? "down" : "up")}");
                break;
            case Sdl.EventGamepadAxisMotion when _pads.TryGetValue(e.Which, out var pad):
                pad.Axes++;
                break;
            case Sdl.EventGamepadTouchpadDown or Sdl.EventGamepadTouchpadUp when _pads.TryGetValue(e.Which, out var pad):
                log.Line($"pad {e.Which}: touchpad {e.TouchpadOrSensor} finger {e.Finger} {(e.Type == Sdl.EventGamepadTouchpadDown ? "down" : "up")} at ({e.TouchX:0.00},{e.TouchY:0.00})");
                if (e.TouchpadOrSensor is 0 or 1) pad.Touch[e.TouchpadOrSensor]++;
                break;
            case Sdl.EventGamepadTouchpadMotion when _pads.TryGetValue(e.Which, out var pad):
                if (e.TouchpadOrSensor is 0 or 1) pad.Touch[e.TouchpadOrSensor]++;
                break;
            case Sdl.EventGamepadSensorUpdate when _pads.TryGetValue(e.Which, out var pad):
                if (e.TouchpadOrSensor == Sdl.SensorGyro)
                {
                    pad.Gyro++;
                    pad.LastGyro = (e.Data0, e.Data1, e.Data2);
                    pad.MaxGyro = Math.Max(pad.MaxGyro, MathF.Sqrt(e.Data0 * e.Data0 + e.Data1 * e.Data1 + e.Data2 * e.Data2));
                }
                else if (e.TouchpadOrSensor == Sdl.SensorAccel) pad.Accel++;
                break;
        }
    }

    private void Open(uint id)
    {
        var handle = Sdl.SDL_OpenGamepad(id);
        if (handle == 0)
        {
            log.Line($"pad {id}: open FAILED: {Sdl.Text(Sdl.SDL_GetError())}");
            return;
        }
        var vendor = Sdl.SDL_GetGamepadVendor(handle);
        var product = Sdl.SDL_GetGamepadProduct(handle);
        var pad = new Pad(handle, Sdl.Text(Sdl.SDL_GetGamepadName(handle)), vendor == 0x28DE);
        _pads[id] = pad;
        var gyro = Sdl.SDL_GamepadHasSensor(handle, Sdl.SensorGyro);
        var accel = Sdl.SDL_GamepadHasSensor(handle, Sdl.SensorAccel);
        log.Line($"pad {id}: \"{pad.Name}\" {vendor:X4}:{product:X4} path {Sdl.Text(Sdl.SDL_GetGamepadPath(handle))} "
                 + $"steam handle {Sdl.SDL_GetGamepadSteamHandle(handle)} touchpads {Sdl.SDL_GetNumGamepadTouchpads(handle)} gyro {gyro} accel {accel}");
        // Only the Steam Controller: Arcadia's rule (#72) is that other pads behave the same with or without Steam.
        if (options.Sensors && pad.IsValve && gyro)
        {
            var on = Sdl.SDL_SetGamepadSensorEnabled(handle, Sdl.SensorGyro, true)
                     & Sdl.SDL_SetGamepadSensorEnabled(handle, Sdl.SensorAccel, true);
            log.Line($"pad {id}: sensors {(on ? "enabled" : "enable FAILED: " + Sdl.Text(Sdl.SDL_GetError()))}");
        }
    }

    private void Report()
    {
        foreach (var (id, pad) in _pads)
        {
            if (pad.Buttons + pad.Axes + pad.Touch[0] + pad.Touch[1] + pad.Gyro + pad.Accel == 0) continue;
            log.Line($"pad {id}: per s buttons {pad.Buttons}, axes {pad.Axes}, touch L {pad.Touch[0]} R {pad.Touch[1]}, "
                     + $"gyro {pad.Gyro} (max {pad.MaxGyro:0.00} rad/s, last {pad.LastGyro.X:0.00}/{pad.LastGyro.Y:0.00}/{pad.LastGyro.Z:0.00}), accel {pad.Accel}");
            pad.Buttons = pad.Axes = pad.Touch[0] = pad.Touch[1] = pad.Gyro = pad.Accel = 0;
            pad.MaxGyro = 0;
        }
    }
}
