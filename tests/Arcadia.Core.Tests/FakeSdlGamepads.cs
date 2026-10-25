using Arcadia.Input;

namespace Arcadia.Tests.Sdl;

/// <summary>
/// SDL3 as a test sets it up: controllers are plugged in and out and hold buttons; what Arcadia opened, rumbled and
/// closed is recorded. Shared by the unit tests and the acceptance tests (linked into <c>Arcadia.App.Tests</c>).
/// </summary>
internal sealed class FakeSdlGamepads : ISdlGamepads
{
    private readonly Lock _lock = new();
    private readonly Queue<(uint Id, bool Added)> _changes = new();
    private readonly Dictionary<uint, PadDevice> _plugged = [];
    private readonly Dictionary<uint, SdlPadState> _states = [];
    private readonly Dictionary<nint, uint> _open = [];
    private uint _nextId = 1;
    private nint _nextHandle = 100;

    /// <summary>Thrown by <see cref="Init"/>, as a missing <c>SDL3.dll</c> would.</summary>
    public Exception? InitError { get; set; }

    /// <summary>Thrown by <see cref="Read"/>, as a native call failing later would.</summary>
    public Exception? ReadError { get; set; }

    public bool IsQuit { get; private set; }

    public int Updates { get; private set; }

    public List<string> Rumbles { get; } = [];

    public List<uint> Closed { get; } = [];

    public IReadOnlyCollection<uint> OpenPads
    {
        get
        {
            lock (_lock)
            {
                return [.. _open.Values];
            }
        }
    }

    /// <returns>SDL's id for the device.</returns>
    public uint Plug(PadDevice device)
    {
        lock (_lock)
        {
            var id = _nextId++;
            _plugged[id] = device;
            _states[id] = default;
            _changes.Enqueue((id, true));
            return id;
        }
    }

    public void Unplug(uint id)
    {
        lock (_lock)
        {
            _plugged.Remove(id);
            _states.Remove(id);
            _changes.Enqueue((id, false));
        }
    }

    /// <summary>Exactly these buttons held on the pad, sticks and triggers at rest.</summary>
    public void Hold(uint id, params SdlPadState.Button[] buttons) =>
        Set(id, new SdlPadState(buttons.Aggregate(0u, (bits, button) => bits | SdlPadState.Bit(button)), 0, 0, 0, 0, 0, 0));

    public void Set(uint id, SdlPadState state)
    {
        lock (_lock)
        {
            _states[id] = state;
        }
    }

    public string Init()
    {
        if (InitError is { } error)
        {
            throw error;
        }

        IsQuit = false;
        return "3.4.18";
    }

    public bool NextDeviceChange(out uint id, out bool added)
    {
        lock (_lock)
        {
            var any = _changes.TryDequeue(out var change);
            (id, added) = change;
            return any;
        }
    }

    public PadDevice Describe(uint id)
    {
        lock (_lock)
        {
            return _plugged.TryGetValue(id, out var device) ? device : default;
        }
    }

    public nint Open(uint id)
    {
        lock (_lock)
        {
            if (!_plugged.ContainsKey(id))
            {
                return 0;
            }

            var handle = _nextHandle++;
            _open[handle] = id;
            return handle;
        }
    }

    public string LastError() => "Gerät nicht gefunden";

    public void Update()
    {
        lock (_lock)
        {
            Updates++;
        }
    }

    public SdlPadState Read(nint pad)
    {
        if (ReadError is { } error)
        {
            throw error;
        }

        lock (_lock)
        {
            return _states.GetValueOrDefault(_open[pad]);
        }
    }

    public void Rumble(nint pad, ushort strength, uint durationMs)
    {
        lock (_lock)
        {
            Rumbles.Add($"pad {_open[pad]}: {strength} for {durationMs} ms");
        }
    }

    public void Close(nint pad)
    {
        lock (_lock)
        {
            if (_open.Remove(pad, out var id))
            {
                Closed.Add(id);
            }
        }
    }

    public void Quit() => IsQuit = true;
}

/// <summary>Controllers as SDL 3.4 describes them (names and USB ids of the real devices).</summary>
internal static class KnownPads
{
    public static readonly PadDevice Xbox360 = new("Xbox 360 Controller", 0x045E, 0x028E, PadType.Xbox360, StickDeviceType.Gamepad);
    public static readonly PadDevice XboxSeries = new("Xbox Series X Controller", 0x045E, 0x0B12, PadType.XboxOne, StickDeviceType.Gamepad);
    public static readonly PadDevice EightBitDoUltimate2 = new("8BitDo Ultimate 2 Wireless Controller for PC", 0x2DC8, 0x310B, PadType.XboxOne, StickDeviceType.Gamepad);
    public static readonly PadDevice DualSense = new("DualSense Wireless Controller", 0x054C, 0x0CE6, PadType.PS5, StickDeviceType.Gamepad);
    public static readonly PadDevice DualShock4 = new("PS4 Controller", 0x054C, 0x09CC, PadType.PS4, StickDeviceType.Gamepad);
    public static readonly PadDevice SwitchPro = new("Nintendo Switch Pro Controller", 0x057E, 0x2009, PadType.SwitchPro, StickDeviceType.Gamepad);
    public static readonly PadDevice JoyConPair = new("Nintendo Switch Joy-Con (L/R)", 0x057E, 0x2008, PadType.JoyConPair, StickDeviceType.Gamepad);
    public static readonly PadDevice JoyConLeft = new("Nintendo Switch Joy-Con (L)", 0x057E, 0x2006, PadType.JoyConLeft, StickDeviceType.Gamepad);
    public static readonly PadDevice SteamVirtual = new("Steam Virtual Gamepad", 0x28DE, 0x11FF, PadType.Xbox360, StickDeviceType.Gamepad);
    public static readonly PadDevice SteamController = new("Steam Controller", 0x28DE, 0x1304, PadType.Steam, StickDeviceType.Gamepad);
    public static readonly PadDevice Wheel = new("Logitech G29 Driving Force Racing Wheel", 0x046D, 0xC24F, PadType.Standard, StickDeviceType.Wheel);
}
