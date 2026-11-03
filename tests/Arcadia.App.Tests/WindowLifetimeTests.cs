using System.Reflection;
using Arcadia.App.Services;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>A closed window lets go of what outlives it: the static error event keeps no disposed window alive.</summary>
public sealed class WindowLifetimeTests
{
    [AvaloniaFact]
    public async Task A_disposed_window_no_longer_listens_to_unexpected_errors()
    {
        var arcadia = await ArcadiaDriver.StartAsync();
        var main = arcadia.Main;
        Assert.Contains(ErrorListeners(), listener => listener.Target == main);

        arcadia.Dispose();

        Assert.DoesNotContain(ErrorListeners(), listener => listener.Target == main);
    }

    private static Delegate[] ErrorListeners() =>
        (typeof(AppErrors).GetField(nameof(AppErrors.Occurred), BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Delegate)?.GetInvocationList() ?? [];
}
