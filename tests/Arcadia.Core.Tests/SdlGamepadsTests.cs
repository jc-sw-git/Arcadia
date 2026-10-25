using Arcadia.Input;

namespace Arcadia.Core.Tests;

/// <summary>Only where Arcadia ships SDL3: the build puts the pinned <c>SDL3.dll</c> next to the tests.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "SDL3.dll ships for Windows only";
        }
    }
}

/// <summary>The real SDL3.dll through Arcadia's P/Invoke: it loads, has every entry point and starts without a controller (CI).</summary>
public sealed class SdlGamepadsTests
{
    [WindowsFact]
    public void The_pinned_SDL3_starts_reads_and_stops()
    {
        var sdl = new SdlGamepads();
        Assert.Equal("3.4.18", sdl.Init());
        try
        {
            // Whatever the machine has connected, every call answers; an unknown id gives no device
            // (SDL 3.4.18 still calls its type Standard, seen on Windows CI).
            while (sdl.NextDeviceChange(out _, out _))
            {
            }

            sdl.Update();
            var unknown = sdl.Describe(uint.MaxValue);
            Assert.Equal((0, 0, StickDeviceType.Unknown), (unknown.Vendor, unknown.Product, unknown.StickType));
            Assert.Equal(0, sdl.Open(uint.MaxValue));
            Assert.NotEmpty(sdl.LastError());
        }
        finally
        {
            sdl.Quit();
        }
    }

    [WindowsFact]
    public void The_source_runs_on_the_real_SDL3_and_reports_no_error()
    {
        var failures = new List<Exception>();
        using var source = new SdlGamepadSource(new SdlGamepads(), _ => { }, failures.Add);

        Assert.True(source.Open());
        source.Poll(0);
        source.Poll(SdlGamepadSource.PollIntervalMs);
        source.Close();

        Assert.Empty(failures);
    }
}
