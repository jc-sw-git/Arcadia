using Arcadia.App.Services;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>#399 throttles only the places that repeat their errors; every other unexpected error is reported each time.</summary>
public sealed class AppErrorsTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T399-9")]
    public async Task An_unexpected_error_elsewhere_is_toasted_and_logged_every_time()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        const string Context = "Test T399-9";
        var toasts = new List<string>();
        void Heard(string message)
        {
            if (message.Contains(Context, StringComparison.Ordinal))
            {
                lock (toasts) toasts.Add(message);
            }
        }

        AppErrors.Occurred += Heard;
        try
        {
            await AppErrors.Guard(() => throw new InvalidOperationException("timer broke"), Context);
            await AppErrors.Guard(() => throw new InvalidOperationException("timer broke"), Context);
        }
        finally
        {
            AppErrors.Occurred -= Heard;
        }

        Assert.Equal(2, toasts.Count);
        var log = await File.ReadAllTextAsync(Path.Combine(AppLog.Directory, "arcadia.log"), TestContext.Current.CancellationToken);
        Assert.Equal(2, log.Split($"Unerwarteter Fehler ({Context}): System.InvalidOperationException: timer broke").Length - 1);
    }
}
