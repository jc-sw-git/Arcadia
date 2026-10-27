namespace SteamInputProbe;

/// <summary>
/// Spike #118: reads every connected controller with SDL3 on its own thread, with background events on, so the UAC
/// probe can show that Arcadia's process would keep seeing the pad (Xbox and the Steam Controller) while the Secure
/// Desktop is up. It only counts activity; the keys that answer UAC go through the VIIPER device, not from here.
/// </summary>
internal sealed class SdlReader : IDisposable
{
    private readonly ProbeLog _log;
    private readonly Thread _thread;
    private volatile bool _stop;

    public SdlReader(ProbeLog log)
    {
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "sdl" };
        _thread.Start();
    }

    private void Run()
    {
        Sdl.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
        Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI", "1");
        Sdl.SDL_SetHint("SDL_JOYSTICK_HIDAPI_STEAM", "1");
        Sdl.SDL_SetHint("SDL_JOYSTICK_ENHANCED_REPORTS", "1");
        Sdl.SDL_SetHint("SDL_JOYSTICK_WGI", "0");
        Sdl.SDL_SetHint("SDL_JOYSTICK_GAMEINPUT", "0");
        if (!Sdl.SDL_Init(Sdl.InitGamepad))
        {
            _log.Line($"SDL reader: SDL_Init FAILED: {Sdl.Text(Sdl.SDL_GetError())}");
            return;
        }

        var pads = new Dictionary<uint, (IntPtr Handle, string Name)>();
        var buttonsPerSecond = new Dictionary<uint, int>();
        var nextReport = Environment.TickCount64 + 1000;
        try
        {
            while (!_stop)
            {
                if (Sdl.SDL_WaitEventTimeout(out var e, 100))
                {
                    switch (e.Type)
                    {
                        case Sdl.EventGamepadAdded:
                            var handle = Sdl.SDL_OpenGamepad(e.Which);
                            if (handle != 0)
                            {
                                var name = Sdl.Text(Sdl.SDL_GetGamepadName(handle));
                                pads[e.Which] = (handle, name);
                                _log.Line($"SDL reader: pad {e.Which} \"{name}\" {Sdl.SDL_GetGamepadVendor(handle):X4}:{Sdl.SDL_GetGamepadProduct(handle):X4} open");
                            }
                            break;
                        case Sdl.EventGamepadRemoved when pads.Remove(e.Which, out var gone):
                            Sdl.SDL_CloseGamepad(gone.Handle);
                            _log.Line($"SDL reader: pad {e.Which} ({gone.Name}) removed");
                            break;
                        case Sdl.EventGamepadButtonDown when pads.ContainsKey(e.Which):
                            buttonsPerSecond[e.Which] = buttonsPerSecond.GetValueOrDefault(e.Which) + 1;
                            var btn = Sdl.Text(Sdl.SDL_GetGamepadStringForButton(e.ButtonOrAxis));
                            _log.Line($"SDL reader: pad {e.Which} button {(btn.Length > 0 ? btn : "#" + e.ButtonOrAxis)} down");
                            break;
                    }
                }
                if (Environment.TickCount64 >= nextReport)
                {
                    foreach (var (id, count) in buttonsPerSecond)
                        if (count > 0) _log.Line($"SDL reader: pad {id} saw {count} button presses in the last second");
                    buttonsPerSecond.Clear();
                    nextReport += 1000;
                }
            }
        }
        finally
        {
            foreach (var pad in pads.Values) Sdl.SDL_CloseGamepad(pad.Handle);
            Sdl.SDL_Quit();
            _log.Line("SDL reader: closed");
        }
    }

    public void Dispose()
    {
        _stop = true;
        _thread.Join(TimeSpan.FromSeconds(2));
    }
}
