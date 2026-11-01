namespace Arcadia.ControllerSupport;

/// <summary>
/// What the old Arcadia (or the user) left of SISR (#214), as the settings' group "SISR" shows it: found, removed on
/// request ("SISR entfernen"), never started. The removal runs as one of <see cref="ControllerSupportService"/>'s actions,
/// under its gate and busy text, so it can't cut into a driver install.
/// </summary>
public sealed class SisrLeftovers
{
    public const string RemovingText = "SISR wird entfernt …";

    private readonly ControllerSupportService _support;

    internal SisrLeftovers(ControllerSupportService support, SisrInstallation? installation)
    {
        _support = support;
        Installation = installation;
    }

    /// <summary>Where SISR is looked for and how it goes; null on other platforms and in tests without it.</summary>
    public SisrInstallation? Installation { get; }

    /// <summary>What of SISR is on the PC, as of the last check; null until checked or without <see cref="Installation"/>.</summary>
    public SisrFound? Found { get; private set; }

    /// <summary>
    /// The group "SISR" shows: SISR or its marker is there, or the last "SISR entfernen" left something (a locked key
    /// folder, the CEF flag) that only its row can name; and the player didn't choose "SISR behalten".
    /// </summary>
    public bool ShowsGroup => Found is not null && (Found.ShowsGroup || RemovalCause is not null) && !_support.Settings.KeepSisr;

    /// <summary>"SISR entfernen" has to close Steam first: the marker is there and Steam runs (it writes it back on exit).</summary>
    public bool RemovalStopsSteam => Found is { HasMarker: true } && _support.SteamRuns;

    /// <summary>Why the last "SISR entfernen" left something, until it runs again; the group row names it.</summary>
    public string? RemovalCause { get; private set; }

    /// <summary>The last "SISR entfernen" stopped before anything went (<see cref="RemovalCause"/> says why).</summary>
    public bool RemovedNothing { get; private set; }

    /// <summary>An SISR runs: beside Pan, two programs on the Steam Controller break both.</summary>
    internal bool IsAnyRunning => Installation?.IsAnyRunning == true;

    /// <summary>"SISR entfernen" removed everything: the settings show the toast and move the focus.</summary>
    public event EventHandler? Removed;

    /// <summary>Why "SISR entfernen" can't close Steam now: it would end <paramref name="game"/>.</summary>
    public static string RemovalBlocker(string game) =>
        $"„{game}“ läuft gerade über Steam. Beende das Spiel zuerst, dann kann Arcadia SISR entfernen.";

    /// <summary>
    /// "SISR entfernen": closes Steam for the marker if it runs (the settings asked), stops SISR, removes what is there
    /// and starts Steam again, also after a failure. What is left stays in the group with its cause.
    /// </summary>
    public Task RemoveAsync(CancellationToken cancellationToken = default) =>
        _support.RunBusyAsync(RemovingText, async token =>
        {
            if (Installation is not { } sisr)
            {
                return;
            }

            // SISR is stopped inside Remove, so only once Steam is closed: a Steam game or a Steam that won't close
            // leaves it running for the game that may be using its pad.
            SisrRemoval result;
            try
            {
                result = await _support.WithSteamClosedAsync(sisr.Find().HasMarker, "„SISR entfernen“", RemovalBlocker,
                    async t =>
                    {
                        _support.SetBusyText(RemovingText);
                        return await Task.Run(() => sisr.Remove(_support.SteamRuns), t);
                    },
                    waitForSteam: _ => true, token);
            }
            catch (InvalidOperationException ex)
            {
                // A Steam game or a Steam that won't close: the group row says why, like any other cause.
                RemovalCause = ex.Message;
                RemovedNothing = true;
                throw;
            }

            Found = sisr.Find();
            RemovalCause = result.Complete && !Found.ShowsGroup ? null : result.Cause ?? SisrInstallation.SteamRunningCause;
            RemovedNothing = result.Cause == SisrInstallation.StopFailedCause;
            _support.Log?.Invoke(result.Complete ? "SISR entfernt" : $"SISR nicht ganz entfernt: {string.Join(", ", result.NotRemoved)} ({result.Cause})");
            if (RemovalCause is null)
            {
                Removed?.Invoke(this, EventArgs.Empty);
            }
        }, cancellationToken);

    /// <summary>
    /// Once at start (#214 decision 6): the old Arcadia kept SISR running across its update, so an SISR it started goes;
    /// one the user started stays. Never throws.
    /// </summary>
    public async Task StopStartedByOldArcadiaAsync(CancellationToken cancellationToken = default)
    {
        if (Installation is not { } sisr)
        {
            return;
        }

        try
        {
            if (await Task.Run(() => sisr.StopStartedByOldArcadia(_support.SteamRuns), cancellationToken) is > 0 and var stopped)
            {
                _support.Log?.Invoke($"SISR beendet, das die frühere Arcadia-Version gestartet hatte ({stopped})");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _support.Log?.Invoke($"SISR der früheren Arcadia-Version ließ sich nicht beenden: {ex}");
        }
    }

    /// <summary>Looks again; true when what is found changed. On another platform nothing is ever found.</summary>
    internal bool Refresh(bool supportedPlatform)
    {
        var before = Found;
        Found = supportedPlatform ? FindOrKeep() : null;
        return Found != before;
    }

    private SisrFound? FindOrKeep()
    {
        try
        {
            return Installation?.Find();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Steam or SISR is writing; the last answer stands.
            return Found;
        }
    }
}
