using System.ComponentModel;
using System.Net;
using Arcadia.ControllerSupport;
using Arcadia.Launchers;
using Arcadia.Tests.Sisr;
using Arcadia.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// The controller emulation's parts (usbip-win2, VIIPER, Pan, HidHide), their fixes with Steam closed when they restart
/// the puck, Pan's update and removal, and SISR's group (#214), against a fake platform and fake time.
/// </summary>
public sealed class ControllerSupportServiceTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakePlatform _platform = new();
    private readonly List<string> _log = [];
    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-sisr-").FullName;
    private readonly HttpClient _http;

    // The built-in launchers as the registry knows them: Steam's games use Steam Input, Epic's don't.
    private static readonly LauncherRegistry Registered = FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Steam), new FakeIntegration(KnownLaunchers.Epic));

    public ControllerSupportServiceTests()
    {
        _http = new HttpClient(new NoNetwork());
    }

    public void Dispose()
    {
        _http.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private ControllerSupportService Create(IViiperInstallation? viiper = null, SisrInstallation? sisr = null, AppSettings? settings = null)
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(settings ?? new AppSettings());
        return new ControllerSupportService(new SettingsService(store), _http, _time, _platform, sisr, _platform)
        {
            Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput, Viiper = viiper,
        };
    }

    private void Log(string line)
    {
        lock (_log)
        {
            _log.Add(line);
        }
    }

    private Game SteamGame()
    {
        var directory = Directory.CreateDirectory(Path.Combine(_directory, "game")).FullName;
        return new Game
        {
            Id = "1",
            LauncherId = "steam",
            Title = "Steam-Spiel",
            Launch = new LaunchTarget.ProtocolUri("steam://rungameid/1"),
            IsInstalled = true,
            InstallDirectory = directory,
        };
    }

    /// <summary>Advances fake time until <paramref name="done"/> holds (bounded, so a bug can't hang the test).</summary>
    private async Task AdvanceUntil(Func<bool> done)
    {
        for (var i = 0; i < 1_000 && !done(); i++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(1);
        }

        Assert.True(done());
    }

    [Fact]
    public void The_blocking_game_is_read_fresh_so_its_end_is_noticed()
    {
        var service = Create();
        _platform.SteamGame = "Hollow Knight";
        Assert.Equal("Hollow Knight", service.SteamGameBlockingInstall());

        _platform.SteamGame = null;

        Assert.Null(service.SteamGameBlockingInstall());
    }

    [Fact]
    public void A_game_counts_only_while_Steam_runs()
    {
        var service = Create();
        _platform.SteamGame = "Hollow Knight";
        _platform.IsSteamRunning = false;

        Assert.Null(service.SteamGameBlockingInstall());
    }

    [Fact]
    public async Task Settings_changed_from_several_threads_at_once_are_all_kept()
    {
        var service = Create();
        var games = Enumerable.Range(0, 40).Select(i => SteamGame() with { Id = $"g{i}" }).ToList();

        await Task.WhenAll(games.Select(game => Task.Run(() => service.SetChoiceFor(game, useArcadiaPad: false))));

        var saved = new SettingsStore(Path.Combine(_directory, "settings.json")).Load();
        Assert.All(games, game => Assert.False(saved.UsesArcadiaPad(game.Key, Registered.GamesUseSteamInput)));
    }

    // --- Pan, the guardian service (#243) ---

    [Fact]
    public async Task A_missing_Pan_is_a_warning_and_its_fix_installs_the_drivers()
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings());
        var service = new ControllerSupportService(new SettingsService(store), _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput };
        _platform.IsPanReady = false;

        await service.RefreshAsync();

        var issue = Assert.Single(service.Issues);
        Assert.Equal("pan-missing", issue.Id);
        Assert.Equal(HealthSeverity.Warning, issue.Severity);
        Assert.Equal(HealthFix.InstallDrivers, issue.Fix);
        Assert.False(service.PanInstalled);

        await service.ApplyFixAsync(HealthFix.InstallDrivers);
        await service.RefreshAsync();

        Assert.Null(service.LastError);
        Assert.Equal(1, _platform.UsbipInstalls);
        Assert.Empty(service.Issues);
        Assert.True(service.PanInstalled);
    }

    [Fact]
    [Trait("TestCase", "T242-27")]
    public async Task Arcadia_s_own_missing_VIIPER_is_fetched_without_a_Windows_prompt()
    {
        var viiper = new FakeViiper();
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings());
        var service = new ControllerSupportService(new SettingsService(store), _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput, Viiper = viiper };

        await service.RefreshAsync();
        Assert.Equal("viiper-missing", Assert.Single(service.Issues).Id);

        await service.ApplyFixAsync(HealthFix.InstallDrivers);
        await service.RefreshAsync();

        Assert.Null(service.LastError);
        Assert.Equal(1, viiper.Installs);
        Assert.Equal(0, _platform.UsbipInstalls);
        Assert.Empty(service.Issues);
    }

    [Fact]
    [Trait("TestCase", "T242-27")]
    public async Task A_VIIPER_download_that_fails_shows_at_its_row()
    {
        var viiper = new FakeViiper { Error = new InvalidOperationException("Prüfsumme von viiper.exe stimmt nicht – Download verworfen.") };
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings());
        var service = new ControllerSupportService(new SettingsService(store), _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput, Viiper = viiper };

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Contains("Prüfsumme", service.LastError);
        Assert.Equal(HealthFix.InstallDrivers, service.FailedFix);
    }

    [Theory]
    [Trait("TestCase", "T242-26")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Neu_installieren_runs_HidHide_s_installer_through_Pan_and_a_declined_prompt_says_so(bool declined)
    {
        var service = Create();
        _platform.IsSteamRunning = false;
        _platform.HidHideVersion = PinnedHidHideDriver;
        _platform.HidHideReinstallDeclined = declined;

        await service.ApplyFixAsync(HealthFix.ReinstallHidHide);

        Assert.Equal(1, _platform.HidHideReinstalls);
        Assert.Equal(0, _platform.UsbipInstalls);
        Assert.Equal(declined, service.LastError?.Contains("„Neu installieren“", StringComparison.Ordinal) ?? false);
    }

    [Fact]
    [Trait("TestCase", "T242-26")]
    public async Task HidHide_is_not_reinstalled_while_Pan_hides_or_holds_the_puck()
    {
        var service = Create();
        _platform.HidHideVersion = PinnedHidHideDriver;
        service.PanHoldsPuck = () => true;

        await service.ApplyFixAsync(HealthFix.ReinstallHidHide);

        Assert.Equal(0, _platform.HidHideReinstalls);
        Assert.Equal(ControllerSupportService.HidHideReinstallWhileHiddenText, service.LastError);
    }

    [Fact]
    [Trait("TestCase", "T296-3")]
    public async Task While_Steam_runs_HidHide_is_reinstalled_with_Steam_closed_and_Steam_starts_again()
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = PinnedHidHideDriver;

        await service.ApplyFixAsync(HealthFix.ReinstallHidHide);

        Assert.Equal(1, _platform.HidHideReinstalls);
        Assert.Equal(0, _platform.PuckRestartsWhileSteamRuns);
        Assert.Equal((1, 1), (_platform.SteamStops, _platform.SteamStarts));
        Assert.True(_platform.IsSteamRunning);
        Assert.Null(service.LastError);
    }

    [Theory]
    [Trait("TestCase", "T296-1")]
    [InlineData(HealthFix.InstallDrivers)]
    [InlineData(HealthFix.ReinstallHidHide)]
    public async Task A_driver_install_that_restarts_the_puck_runs_only_with_Steam_closed(HealthFix fix)
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = fix == HealthFix.ReinstallHidHide ? PinnedHidHideDriver : null;

        Assert.True(service.StopsSteamFor(fix));
        await service.ApplyFixAsync(fix);

        Assert.Equal(1, _platform.UsbipInstalls + _platform.HidHideReinstalls);
        Assert.Equal(0, _platform.PuckRestartsWhileSteamRuns);
        Assert.Equal((1, 1), (_platform.SteamStops, _platform.SteamStarts));
        Assert.Null(service.LastError);
    }

    [Fact]
    [Trait("TestCase", "T296-2")]
    public async Task Without_HidHide_s_installer_Steam_is_neither_closed_nor_asked_about()
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.IsUsbipReady = false;

        Assert.False(service.StopsSteamFor(HealthFix.InstallDrivers));
        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(1, _platform.UsbipInstalls);
        Assert.Equal((0, 0), (_platform.SteamStops, _platform.SteamStarts));
        Assert.Null(service.LastError);
    }

    [Fact]
    [Trait("TestCase", "T296-2")]
    public async Task A_HidHide_whose_removal_waits_for_a_restart_doesn_t_close_Steam()
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = null;
        _platform.HidHideRemovalPending = true;

        Assert.False(service.StopsSteamFor(HealthFix.InstallDrivers));
        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(1, _platform.UsbipInstalls);
        Assert.Equal(0, _platform.SteamStops);
    }

    [Theory]
    [Trait("TestCase", "T296-5")]
    [InlineData(HealthFix.InstallDrivers, "declined")]
    [InlineData(HealthFix.InstallDrivers, "failed")]
    [InlineData(HealthFix.ReinstallHidHide, "declined")]
    public async Task Steam_closed_for_a_driver_install_starts_again_when_the_install_fails(HealthFix fix, string outcome)
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = fix == HealthFix.ReinstallHidHide ? PinnedHidHideDriver : null;
        _platform.UsbipInstallDeclined = outcome == "declined";
        _platform.HidHideReinstallDeclined = outcome == "declined";
        _platform.UsbipInstallError = outcome == "failed" ? new IOException("Download abgebrochen") : null;

        await service.ApplyFixAsync(fix);

        Assert.NotNull(service.LastError);
        Assert.Equal((1, 1), (_platform.SteamStops, _platform.SteamStarts));
        Assert.True(_platform.IsSteamRunning);
    }

    [Fact]
    [Trait("TestCase", "T296-5")]
    public async Task Steam_closed_for_a_driver_install_starts_again_when_the_install_is_cancelled()
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.HidHideVersion = null;
        using var cancel = new CancellationTokenSource();
        _platform.DuringDriverInstall = cancel.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ApplyFixAsync(HealthFix.InstallDrivers, cancel.Token));

        Assert.Equal((1, 1), (_platform.SteamStops, _platform.SteamStarts));
    }

    [Theory]
    [Trait("TestCase", "T296-6")]
    [InlineData(HealthFix.InstallDrivers)]
    [InlineData(HealthFix.ReinstallHidHide)]
    public async Task A_Steam_that_won_t_close_stops_the_driver_install_before_anything_is_installed(HealthFix fix)
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.SteamRefusesToExit = true;
        _platform.HidHideVersion = fix == HealthFix.ReinstallHidHide ? PinnedHidHideDriver : null;

        await service.ApplyFixAsync(fix);

        Assert.Equal(ControllerSupportService.SteamWontCloseText, service.LastError);
        Assert.Equal(0, _platform.UsbipInstalls + _platform.HidHideReinstalls);
        Assert.Equal(0, _platform.SteamStarts);
    }

    [Fact]
    [Trait("TestCase", "T296-4")]
    public async Task A_running_Steam_game_stops_a_driver_install_that_would_close_Steam()
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.SteamGame = "Hollow Knight";
        _platform.HidHideVersion = null;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Equal(ControllerSupportService.DriverInstallBlocker("Hollow Knight"), service.LastError);
        Assert.Equal((0, 0), (_platform.UsbipInstalls, _platform.SteamStops));
    }

    [Fact]
    [Trait("TestCase", "T296-1")]
    public async Task A_Steam_slow_to_come_back_after_a_driver_install_doesn_t_fail_the_install()
    {
        var service = Create();
        _platform.IsSteamRunning = true;
        _platform.SteamWontStart = true;
        _platform.HidHideVersion = null;

        var install = service.ApplyFixAsync(HealthFix.InstallDrivers);
        await AdvanceUntil(() => install.IsCompleted);
        await install;

        Assert.Null(service.LastError);
        Assert.Equal(1, _platform.SteamStarts);
        Assert.Null(service.BusyText);
    }

    [Theory]
    [Trait("TestCase", "T242-26")]
    [InlineData("1.5.0.0")]
    [InlineData("1.4.180.0")]
    [InlineData(null)]
    public async Task HidHide_is_reinstalled_only_over_the_pinned_version(string? installed)
    {
        var service = Create();
        _platform.IsSteamRunning = false;
        _platform.HidHideVersion = installed is null ? null : Version.Parse(installed);

        await service.ApplyFixAsync(HealthFix.ReinstallHidHide);

        Assert.Equal(0, _platform.HidHideReinstalls);
        Assert.Contains("andere Version", service.LastError);
    }

    private static readonly Version PinnedHidHideDriver = new(1, 4, 181, 0);

    private sealed class FakeViiper : IViiperInstallation
    {
        public bool IsInstalled { get; private set; }

        public int Installs { get; private set; }

        public Exception? Error { get; init; }

        public Task InstallAsync(HttpClient http, CancellationToken cancellationToken)
        {
            Installs++;
            if (Error is not null)
            {
                throw Error;
            }

            IsInstalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    [Trait("TestCase", "T243-33")]
    public async Task A_missing_VIIPER_is_a_part_row_and_its_fix_brings_it_without_a_Windows_prompt()
    {
        var service = Create(new FakeViiper());

        await service.RefreshAsync();

        var issue = Assert.Single(service.Issues);
        Assert.Equal("viiper-missing", issue.Id);
        Assert.Equal(HealthFix.InstallDrivers, issue.Fix);

        await service.ApplyFixAsync(HealthFix.InstallDrivers);
        await service.RefreshAsync();

        Assert.Null(service.LastError);
        Assert.Equal(0, _platform.UsbipInstalls);
        Assert.Empty(service.Issues);
    }

    [Fact]
    [Trait("TestCase", "T243-32")]
    public async Task A_missing_HidHide_is_a_part_row_and_the_one_install_brings_it()
    {
        var service = Create();
        _platform.HidHideVersion = null;

        await service.RefreshAsync();

        var issue = Assert.Single(service.Issues);
        Assert.Equal("hidhide-missing", issue.Id);
        Assert.Equal(HealthFix.InstallDrivers, issue.Fix);
        Assert.Equal("Installieren", issue.FixLabel);

        await service.ApplyFixAsync(HealthFix.InstallDrivers);
        await service.RefreshAsync();

        Assert.Null(service.LastError);
        Assert.Equal(1, _platform.UsbipInstalls);
        Assert.Equal(Arcadia.Pan.HidHideRelease.Version, _platform.HidHideVersion);
        Assert.Empty(service.Issues);
    }

    [Theory]
    [Trait("TestCase", "T243-34")]
    [InlineData("1.1.0", "Aktualisieren")]
    [InlineData("2.0.1", null)]
    public async Task An_unsupported_HidHide_is_updated_only_when_it_is_older_than_Pan_supports(string version, string? fixLabel)
    {
        var service = Create();
        _platform.HidHideVersion = Version.Parse(version);

        await service.RefreshAsync();

        var issue = Assert.Single(service.Issues);
        Assert.Equal("hidhide-unsupported", issue.Id);
        Assert.Equal("HidHide-Version wird nicht unterstützt", issue.Title);
        Assert.Equal(fixLabel, issue.FixLabel);
        Assert.Equal(fixLabel is null ? HealthFix.None : HealthFix.InstallDrivers, issue.Fix);
        Assert.Equal(fixLabel is not null, ControllerSupportPlatform.HidHideNeedsInstaller(_platform.HidHideVersion));
    }

    [Fact]
    [Trait("TestCase", "T243-35")]
    public async Task While_the_settings_watch_Pan_s_answer_about_hiding_becomes_a_row_and_goes_again()
    {
        var service = Create();
        var pan = new Arcadia.Tests.Pads.FakeEmulationGuardian { Hiding = Arcadia.Pan.PanReason.HidHideBusy };
        service.HidingCheck = pan;

        // Closed settings don't ask Pan: a pipe round trip and HidHide's lock for nothing.
        await service.TickAsync();
        Assert.Empty(service.Issues);
        Assert.Equal(0, pan.HidingChecks);

        service.WatchSetup = true;
        await service.TickAsync();
        var busy = Assert.Single(service.Issues);
        Assert.Equal("hidhide-busy", busy.Id);
        Assert.Equal("HidHide-Konfiguration ist geöffnet", busy.Title);
        Assert.Contains("Bitte schließe sie", busy.Detail);

        pan.Hiding = Arcadia.Pan.PanReason.None;
        await service.TickAsync();
        Assert.Empty(service.Issues);
    }

    [Fact]
    [Trait("TestCase", "T243-35")]
    public async Task Pan_s_answer_about_hiding_goes_with_the_settings_as_nobody_asks_again()
    {
        var service = Create();
        var pan = new Arcadia.Tests.Pads.FakeEmulationGuardian { Hiding = Arcadia.Pan.PanReason.HidHideBusy };
        service.HidingCheck = pan;
        service.WatchSetup = true;
        await service.TickAsync();
        Assert.Equal(["hidhide-busy"], service.Issues.Select(i => i.Id));

        // The user closes the settings and later HidHide's configuration: the header must not keep "Bitte schließe sie".
        service.WatchSetup = false;
        await service.TickAsync();

        Assert.Empty(service.Issues);
    }

    [Fact]
    [Trait("TestCase", "T243-35")]
    public async Task Pan_is_not_asked_about_hiding_while_HidHide_or_Pan_is_missing()
    {
        var service = Create();
        var pan = new Arcadia.Tests.Pads.FakeEmulationGuardian { Hiding = Arcadia.Pan.PanReason.HidHideBusy };
        service.HidingCheck = pan;
        service.WatchSetup = true;
        _platform.HidHideVersion = null;

        await service.TickAsync();
        Assert.Equal("hidhide-missing", Assert.Single(service.Issues).Id);

        _platform.HidHideVersion = Arcadia.Pan.HidHideRelease.Version;
        _platform.IsPanReady = false;
        await service.TickAsync();
        Assert.Equal("pan-missing", Assert.Single(service.Issues).Id);
        Assert.Equal(0, pan.HidingChecks);
    }

    [Fact]
    public async Task The_tick_shows_the_driver_warning_while_the_settings_watch()
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings());
        var service = new ControllerSupportService(new SettingsService(store), _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput };
        _platform.IsPanReady = false;

        // The first tick looks once, for the header; closed settings don't look again (registry, service list).
        await service.TickAsync();
        Assert.Equal("pan-missing", Assert.Single(service.Issues).Id);

        _platform.IsPanReady = true;
        await service.TickAsync();
        Assert.Equal("pan-missing", Assert.Single(service.Issues).Id);

        service.WatchSetup = true;
        await service.TickAsync();
        Assert.Empty(service.Issues);

        _platform.IsPanReady = false;
        await service.TickAsync();
        Assert.Equal("pan-missing", Assert.Single(service.Issues).Id);
    }

    [Fact]
    public async Task A_missing_Pan_is_a_warning_while_the_option_is_off()
    {
        // Owner's test 2026-10-26: the header showed nothing while the switch was off, the settings did.
        var service = Create();
        _platform.IsPanReady = false;

        await service.RefreshAsync();

        Assert.Equal("pan-missing", Assert.Single(service.Issues).Id);
    }

    [Fact]
    [Trait("TestCase", "T243-32")]
    public async Task A_usbip_install_whose_HidHide_step_failed_still_records_the_restart_it_wants()
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings());
        var settings = new SettingsService(store);
        var service = new ControllerSupportService(settings, _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput };
        _platform.IsUsbipReady = false;
        _platform.HidHideVersion = null;
        _platform.HidHideInstallError = new InvalidOperationException("Pan ist eingerichtet, HidHide aber nicht");

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Contains("HidHide aber nicht", service.LastError);
        Assert.Equal(_time.GetUtcNow(), settings.Current.UsbipInstalledAt);
    }

    [Fact]
    public async Task A_declined_driver_prompt_fails_the_fix_visibly()
    {
        var service = Create();
        _platform.IsPanReady = false;
        _platform.UsbipInstallDeclined = true;

        await service.ApplyFixAsync(HealthFix.InstallDrivers);

        Assert.Contains("abgelehnt", service.LastError);
        Assert.Equal(HealthFix.InstallDrivers, service.FailedFix);
        Assert.False(_platform.IsPanReady);

        service.ClearError();
        Assert.Null(service.FailedFix);
    }

    [Fact]
    public async Task Restoring_the_controllers_runs_Pans_restore()
    {
        var service = Create();
        var restored = 0;
        service.ControllersRestored += (_, _) => restored++;

        await service.RestoreControllersAsync();

        Assert.Null(service.LastError);
        Assert.Equal(1, _platform.Restores);
        // The pad may count afresh (T243-40).
        Assert.Equal(1, restored);
    }

    [Fact]
    [Trait("TestCase", "T366-1")]
    public async Task Restoring_the_controllers_lets_go_of_Pan_first_and_takes_it_up_again_afterwards()
    {
        // #366: --restore runs beside the service; without Arcadia's client nobody makes Pan hide meanwhile.
        var service = Create();
        var steps = new List<string>();
        service.LetGoOfPan = _ =>
        {
            steps.Add($"let go, left alone: {service.WhyPanIsLeftAlone}");
            return Task.CompletedTask;
        };
        _platform.OnRestore = () => steps.Add($"restore, left alone: {service.WhyPanIsLeftAlone}");

        await service.RestoreControllersAsync();

        Assert.Equal(
            [$"let go, left alone: {ControllerSupportService.RestoringReason}", $"restore, left alone: {ControllerSupportService.RestoringReason}"],
            steps);
        Assert.True(service.MayTalkToPan, "the emulation comes back after the restore");
        Assert.False(service.IsRestoringControllers);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_declined_or_failed_restore_still_takes_Pan_up_again(bool declined, bool fails)
    {
        var service = Create();
        _platform.RestoreDeclined = declined;
        if (fails)
        {
            _platform.OnRestore = () => throw new InvalidOperationException("Pan konnte nicht alles aufräumen");
        }

        await service.RestoreControllersAsync();

        Assert.NotNull(service.LastError);
        Assert.True(service.MayTalkToPan);
    }

    [Fact]
    public async Task A_restore_waits_until_Arcadia_has_let_go_of_Pan()
    {
        var service = Create();
        var letGo = new TaskCompletionSource();
        service.LetGoOfPan = _ => letGo.Task;

        var restore = service.RestoreControllersAsync();

        Assert.Equal(0, _platform.Restores);
        Assert.False(service.MayTalkToPan);
        letGo.SetResult();
        await restore;
        Assert.Equal(1, _platform.Restores);
    }

    [Fact]
    public async Task A_declined_restore_prompt_is_reported()
    {
        var service = Create();
        _platform.RestoreDeclined = true;
        var restored = 0;
        service.ControllersRestored += (_, _) => restored++;

        await service.RestoreControllersAsync();

        Assert.Contains("abgelehnt", service.LastError);
        Assert.Equal(0, restored);
    }

    [Theory]
    [InlineData(Arcadia.ControllerSupport.Pan.PanCompatibility.UpdateRequired, true)]
    [InlineData(Arcadia.ControllerSupport.Pan.PanCompatibility.Compatible, false)]
    [InlineData(Arcadia.ControllerSupport.Pan.PanCompatibility.NotInstalled, false)]
    [InlineData(Arcadia.ControllerSupport.Pan.PanCompatibility.NotBundled, false)]
    public void Only_a_Pan_that_differs_from_the_bundled_one_makes_the_update_due(Arcadia.ControllerSupport.Pan.PanCompatibility compatibility, bool due)
    {
        _platform.PanCompatibility = compatibility;
        var service = Create();

        Assert.Equal(due, service.CheckPanAtStart());
        Assert.Equal(due, service.IsPanUpdateDue);
    }

    [Fact]
    public void Off_Windows_Pan_is_never_checked()
    {
        _platform.IsSupported = false;
        _platform.PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.UpdateRequired;

        Assert.False(Create().CheckPanAtStart());
    }

    [Fact]
    public async Task The_Pan_update_clears_the_due_flag_only_once_it_is_done()
    {
        _platform.PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.UpdateRequired;
        var service = Create();
        service.CheckPanAtStart();

        _platform.PanUpdateAnswer = false;
        Assert.False(await service.UpdatePanAsync());
        Assert.True(service.IsPanUpdateDue);

        _platform.PanUpdateAnswer = true;
        Assert.True(await service.UpdatePanAsync());
        Assert.False(service.IsPanUpdateDue);
        Assert.Equal(2, _platform.PanUpdates);
        Assert.Equal(0, _platform.UsbipInstalls);
    }

    [Fact]
    public async Task A_failing_Pan_update_throws_and_stays_due()
    {
        _platform.PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.UpdateRequired;
        _platform.PanUpdateError = new InvalidOperationException("Pan --install meldete Fehler 3");
        var service = Create();
        service.CheckPanAtStart();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePanAsync());

        Assert.Contains("Fehler 3", error.Message);
        Assert.True(service.IsPanUpdateDue);
    }

    [Fact]
    public void A_declined_update_switches_Pan_off_and_Arcadia_then_leaves_the_old_Pan_alone()
    {
        // #326: an old Pan without a client gives everything back with its own code and ends itself.
        _platform.PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.UpdateRequired;
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings { PanEnabled = true });
        var settings = new SettingsService(store);
        var service = new ControllerSupportService(settings, _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput };
        service.CheckPanAtStart();
        Assert.True(service.MayTalkToPan, "on, the first hello still reaches Pan: a firmware hold shows first");

        service.SwitchPanOffForUpdate("Pan --install meldete Fehler 3");

        Assert.False(settings.Current.PanEnabled);
        Assert.True(service.IsPanUpdateDue, "the switch offers the update again");
        Assert.False(service.MayTalkToPan);
        Assert.Equal("Pan --install meldete Fehler 3", service.PanUpdateError);
    }

    [Fact]
    public async Task A_done_update_lets_Arcadia_talk_to_Pan_again_and_forgets_the_last_error()
    {
        _platform.PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.UpdateRequired;
        var service = Create();
        service.CheckPanAtStart();
        service.SwitchPanOffForUpdate("Fehler 3");

        Assert.True(await service.UpdatePanAsync());

        Assert.True(service.MayTalkToPan);
        Assert.Null(service.PanUpdateError);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task While_the_update_runs_Arcadia_leaves_Pan_alone_so_it_does_not_restart_the_old_service(bool succeeds)
    {
        // --install stops the old service and waits for it to end; a client that reconnected restarted it 90 ms later,
        // and --install gave up after 90 s with exit code 3.
        _platform.PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.UpdateRequired;
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings { PanEnabled = true });
        var service = new ControllerSupportService(new SettingsService(store), _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput };
        service.CheckPanAtStart();
        string? whyDuringUpdate = null;
        _platform.WhilePanUpdates = () => whyDuringUpdate = service.WhyPanIsLeftAlone;
        _platform.PanUpdateError = succeeds ? null : new InvalidOperationException("Pan --install meldete Fehler 3");

        if (succeeds)
        {
            Assert.True(await service.UpdatePanAsync());
            Assert.True(service.MayTalkToPan, "the new Pan is talked to at once");
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePanAsync());
            Assert.Equal(ControllerSupportService.PanUpdatingReason, service.WhyPanIsLeftAlone);
            service.SwitchPanOffForUpdate("Pan --install meldete Fehler 3");
            Assert.Equal(ControllerSupportService.OldPanOffReason, service.WhyPanIsLeftAlone);
        }

        Assert.Equal(ControllerSupportService.PanUpdatingReason, whyDuringUpdate);
        Assert.False(service.IsPanUpdating);
    }

    [Fact]
    public async Task Declined_the_update_Arcadia_leaves_Pan_alone_until_it_is_switched_off()
    {
        // #354: between the declined prompt and the switch-off, a reconnecting client would reach the old Pan.
        _platform.PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.UpdateRequired;
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings { PanEnabled = true });
        var service = new ControllerSupportService(new SettingsService(store), _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput };
        service.CheckPanAtStart();
        _platform.PanUpdateAnswer = false;

        Assert.False(await service.UpdatePanAsync());
        Assert.Equal(ControllerSupportService.PanUpdatingReason, service.WhyPanIsLeftAlone);

        service.SwitchPanOffForUpdate(null);
        Assert.False(service.IsPanUpdating);
        Assert.Equal(ControllerSupportService.OldPanOffReason, service.WhyPanIsLeftAlone);
    }

    [Fact]
    public void Without_a_due_update_Arcadia_talks_to_Pan_also_while_it_is_off()
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Save(new AppSettings { PanEnabled = false });
        var service = new ControllerSupportService(new SettingsService(store), _http, _time, _platform, steam: _platform) { Log = Log, GamesUseSteamInput = Registered.GamesUseSteamInput };
        service.CheckPanAtStart();

        Assert.True(service.MayTalkToPan);
    }

    [Fact]
    public async Task The_restore_after_the_old_Pan_says_whether_the_prompt_was_confirmed()
    {
        var service = Create();

        _platform.RestoreDeclined = true;
        Assert.False(await service.RestoreAfterOldPanAsync());
        _platform.RestoreDeclined = false;
        Assert.True(await service.RestoreAfterOldPanAsync());
        Assert.Equal(2, _platform.Restores);
    }

    // --- SISR, removed on request or kept (#214) ---

    [Fact]
    [Trait("TestCase", "T214-1")]
    [Trait("TestCase", "T214-6")]
    public async Task The_group_SISR_shows_while_SISR_is_there_and_goes_with_SISR_behalten()
    {
        using var pc = new FakeSisrPc().WithSisr();
        var service = Create(sisr: pc.Installation());

        await service.RefreshAsync();
        Assert.True(service.Sisr.ShowsGroup);

        var kept = Create(sisr: pc.Installation(), settings: new AppSettings { KeepSisr = true });
        await kept.RefreshAsync();
        Assert.False(kept.Sisr.ShowsGroup);
    }

    [Fact]
    [Trait("TestCase", "T214-3")]
    public async Task With_Steam_closed_SISR_entfernen_removes_everything_without_touching_Steam()
    {
        using var pc = new FakeSisrPc().WithSisr();
        _platform.IsSteamRunning = false;
        var service = Create(sisr: pc.Installation());
        await service.RefreshAsync();
        var removed = 0;
        service.Sisr.Removed += (_, _) => removed++;
        Assert.False(service.Sisr.RemovalStopsSteam);

        await service.Sisr.RemoveAsync();

        Assert.Null(service.LastError);
        Assert.Null(service.Sisr.RemovalCause);
        Assert.Equal(1, removed);
        Assert.False(service.Sisr.ShowsGroup);
        Assert.False(File.Exists(pc.SisrExe));
        Assert.False(File.Exists(pc.CefFlag));
        Assert.Equal(0, _platform.SteamStops + _platform.SteamStarts);
    }

    [Fact]
    [Trait("TestCase", "T214-4")]
    public async Task With_Steam_running_the_marker_goes_with_Steam_closed_and_Steam_starts_again()
    {
        using var pc = new FakeSisrPc().WithSisr();
        var service = Create(sisr: pc.Installation());
        await service.RefreshAsync();
        Assert.True(service.Sisr.RemovalStopsSteam);

        await service.Sisr.RemoveAsync();

        Assert.Null(service.LastError);
        Assert.Equal(1, _platform.SteamStops);
        Assert.Equal(1, _platform.SteamStarts);
        Assert.Equal(0, pc.Installation().Find().MarkerAccounts);
        Assert.False(service.Sisr.ShowsGroup);
    }

    [Fact]
    [Trait("TestCase", "T214-4")]
    public async Task A_Steam_that_won_t_close_or_a_Steam_game_leaves_SISR_as_it_is()
    {
        using var pc = new FakeSisrPc().WithSisr();
        pc.System.RunSisr(pc.SisrExe, id: 5);
        var service = Create(sisr: pc.Installation());
        await service.RefreshAsync();

        _platform.SteamGame = "Hades II";
        await service.Sisr.RemoveAsync();
        Assert.Equal(SisrLeftovers.RemovalBlocker("Hades II"), service.LastError);
        Assert.Equal(service.LastError, service.Sisr.RemovalCause);
        Assert.True(service.Sisr.RemovedNothing);

        _platform.SteamGame = null;
        _platform.SteamRefusesToExit = true;
        await service.Sisr.RemoveAsync();
        Assert.Equal(ControllerSupportService.SteamWontCloseText, service.LastError);
        Assert.Equal(service.LastError, service.Sisr.RemovalCause);

        // The game may be using SISR's pad: SISR is stopped only once Steam is closed.
        Assert.Empty(pc.System.Stopped);
        Assert.True(File.Exists(pc.SisrExe));
        Assert.Equal(1, pc.Installation().Find().MarkerAccounts);
        Assert.Equal(0, _platform.SteamStarts);
        Assert.True(service.Sisr.ShowsGroup);
    }

    [Fact]
    [Trait("TestCase", "T214-8")]
    public async Task An_SISR_that_won_t_stop_keeps_its_files_names_the_cause_and_Steam_stays()
    {
        using var pc = new FakeSisrPc().WithSisr();
        pc.System.RunSisr(pc.SisrExe, id: 5);
        pc.System.Unstoppable.Add(5);
        var service = Create(sisr: pc.Installation());
        await service.RefreshAsync();

        await service.Sisr.RemoveAsync();

        Assert.Equal(SisrInstallation.StopFailedCause, service.Sisr.RemovalCause);
        Assert.True(service.Sisr.RemovedNothing);
        Assert.True(File.Exists(pc.SisrExe));
        // Steam closed first (SISR is stopped only then) and came back.
        Assert.Equal(1, _platform.SteamStops);
        Assert.Equal(1, _platform.SteamStarts);

        // Idempotent: once SISR can stop, the next try finishes the job.
        pc.System.Unstoppable.Clear();
        await service.Sisr.RemoveAsync();
        Assert.Null(service.Sisr.RemovalCause);
        Assert.False(service.Sisr.ShowsGroup);
    }

    [Fact]
    [Trait("TestCase", "T214-8")]
    public async Task What_is_left_after_program_and_marker_went_keeps_the_group_until_a_retry_removes_it()
    {
        using var pc = new FakeSisrPc().WithSisr();
        _platform.IsSteamRunning = false;
        var service = Create(sisr: pc.Installation());
        await service.RefreshAsync();
        var removed = 0;
        service.Sisr.Removed += (_, _) => removed++;

        pc.System.HasUninstallEntry = true;
        pc.System.UninstallEntryLocked = true;
        await service.Sisr.RemoveAsync();
        Assert.False(service.Sisr.Found!.ShowsGroup);

        // Only SISR's uninstall entry is left; it alone wouldn't show the group, so the cause keeps it.
        Assert.NotNull(service.Sisr.RemovalCause);
        Assert.False(service.Sisr.RemovedNothing);
        Assert.True(service.Sisr.ShowsGroup);
        Assert.Equal(0, removed);

        pc.System.UninstallEntryLocked = false;
        await service.Sisr.RemoveAsync();
        Assert.Null(service.Sisr.RemovalCause);
        Assert.False(service.Sisr.ShowsGroup);
        Assert.Equal(1, removed);
    }

    [Fact]
    [Trait("TestCase", "T214-7")]
    public async Task SISR_laeuft_shows_under_Pan_while_the_settings_watch_also_with_the_group_hidden_and_not_with_Pan_off()
    {
        using var pc = new FakeSisrPc().WithSisr();
        pc.System.RunSisr(pc.SisrExe);
        var service = Create(sisr: pc.Installation(), settings: new AppSettings { KeepSisr = true });
        service.WatchSetup = true;

        await service.TickAsync();
        Assert.Contains(EmulationParts.SisrRunning, service.Issues);

        // The header doesn't keep it once the settings close; nobody looks at the process list then.
        service.WatchSetup = false;
        await service.TickAsync();
        Assert.DoesNotContain(EmulationParts.SisrRunning, service.Issues);

        var panOff = Create(sisr: pc.Installation(), settings: new AppSettings { PanEnabled = false });
        panOff.WatchSetup = true;
        await panOff.TickAsync();
        Assert.DoesNotContain(EmulationParts.SisrRunning, panOff.Issues);
    }

    [Fact]
    [Trait("TestCase", "T214-10")]
    public async Task At_start_only_an_SISR_the_older_Arcadia_started_is_stopped()
    {
        using var pc = new FakeSisrPc().WithSisr();
        var log = Path.Combine(pc.LogDirectory, "sisr.log");
        pc.System.RunSisr(pc.SisrExe, id: 5, $"\"{pc.SisrExe}\" {SisrInstallation.ArcadiaApiArgument} --log.file \"{log}\"");
        pc.System.RunSisr(pc.SisrExe, id: 6);
        var service = Create(sisr: pc.Installation());

        await service.Sisr.StopStartedByOldArcadiaAsync();

        Assert.Equal([5], pc.System.Stopped);
        // Steam runs, so it drops the layout SISR forced; a kill skips SISR's own reset.
        Assert.Equal(1, pc.System.SteamLayoutResets);
    }

    /// <summary>Nothing here goes to the network; a request that does is a bug.</summary>
    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException($"Unerwartete Anfrage: {request.RequestUri}");
    }

    private sealed class FakePlatform : IControllerSupportPlatform, ILauncherProgram
    {
        // Steam the program, as the controller support asks it.
        string? ILauncherProgram.Folder => null;

        bool ILauncherProgram.IsRunning => IsSteamRunning;

        bool ILauncherProgram.IsReady => IsSteamReady;

        RunningLauncherGame? ILauncherProgram.RunningGame() => RunningSteamGame() is { } name ? new("0", name) : null;

        Task<bool> ILauncherProgram.CloseAsync(TimeProvider time, CancellationToken cancellationToken) => StopSteamAsync(time, cancellationToken);

        void ILauncherProgram.Start() => StartSteam();

        public bool IsSupported { get; set; } = true;

        public bool IsSteamRunning { get; set; } = true;

        public bool IsSteamReady => IsSteamRunning;

        /// <summary>"Steam starten" does nothing, as with a broken Steam.</summary>
        public bool SteamWontStart { get; set; }

        public bool IsUsbipReady { get; set; } = true;

        public string? SteamGame { get; set; }

        public bool SteamRefusesToExit { get; set; }

        public bool UsbipInstallDeclined { get; set; }

        public int SteamStops { get; private set; }

        public int SteamStarts { get; private set; }

        public int UsbipInstalls { get; private set; }

        public string? RunningSteamGame() => SteamGame;

        public Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken)
        {
            if (SteamRefusesToExit)
            {
                return Task.FromResult(false);
            }

            SteamStops++;
            IsSteamRunning = false;
            return Task.FromResult(true);
        }

        public void StartSteam()
        {
            SteamStarts++;
            IsSteamRunning = !SteamWontStart;
        }

        public Exception? UsbipInstallError { get; set; }

        /// <summary>Set, the install sets up usbip-win2 and Pan, then fails as Pan's exit code 6 does (HidHide not installed).</summary>
        public Exception? HidHideInstallError { get; set; }

        public bool IsPanReady { get; set; } = true;

        public Arcadia.ControllerSupport.Pan.PanCompatibility PanCompatibility { get; set; } = Arcadia.ControllerSupport.Pan.PanCompatibility.Compatible;

        /// <summary>What the Pan-only update answers (#297): true done, false declined; a set error is thrown.</summary>
        public bool PanUpdateAnswer { get; set; } = true;

        public Exception? PanUpdateError { get; set; }

        public int PanUpdates { get; private set; }

        /// <summary>Runs while Pan's <c>--install</c> would, to see what Arcadia does meanwhile.</summary>
        public Action? WhilePanUpdates { get; set; }

        public Task<bool> UpdatePanAsync(CancellationToken cancellationToken)
        {
            PanUpdates++;
            WhilePanUpdates?.Invoke();
            if (PanUpdateError is { } error)
            {
                throw error;
            }

            if (PanUpdateAnswer)
            {
                PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.Compatible;
            }

            return Task.FromResult(PanUpdateAnswer);
        }

        /// <summary>HidHide's version (#243 B2); a supported one unless a test takes it away.</summary>
        public Version? HidHideVersion { get; set; } = Arcadia.Pan.HidHideRelease.Version;

        public int Restores { get; private set; }

        public int HidHideReinstalls { get; private set; }

        public bool HidHideReinstallDeclined { get; set; }

        /// <summary>HidHide's installer ran (and with it the puck's restart) while Steam ran (#296): never should.</summary>
        public int PuckRestartsWhileSteamRuns { get; private set; }

        /// <summary>HidHide was uninstalled and goes at the next restart: its installer doesn't run until then.</summary>
        public bool HidHideRemovalPending { get; set; }

        public bool InstallsHidHide => ControllerSupportPlatform.HidHideNeedsInstaller(HidHideVersion) && !HidHideRemovalPending;

        /// <summary>Runs inside the driver install, e.g. to cancel it midway.</summary>
        public Action? DuringDriverInstall { get; set; }

        public Task<bool> ReinstallHidHideAsync(HttpClient http, CancellationToken cancellationToken)
        {
            HidHideReinstalls++;
            PuckRestartsWhileSteamRuns += IsSteamRunning ? 1 : 0;
            return Task.FromResult(!HidHideReinstallDeclined);
        }

        public Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken)
        {
            UsbipInstalls++;
            PuckRestartsWhileSteamRuns += IsSteamRunning && InstallsHidHide ? 1 : 0;
            DuringDriverInstall?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (UsbipInstallError is { } error)
            {
                throw error;
            }

            IsUsbipReady = !UsbipInstallDeclined;
            IsPanReady = !UsbipInstallDeclined;
            if (HidHideInstallError is { } hidHideError)
            {
                throw hidHideError;
            }

            if (!UsbipInstallDeclined && InstallsHidHide)
            {
                HidHideVersion = Arcadia.Pan.HidHideRelease.Version;
            }

            return Task.FromResult(!UsbipInstallDeclined);
        }

        public bool RestoreDeclined { get; set; }

        /// <summary>Runs as the restore starts, to look at the service in that moment.</summary>
        public Action? OnRestore { get; set; }

        public Task<bool> RestoreControllersAsync(CancellationToken cancellationToken)
        {
            Restores++;
            OnRestore?.Invoke();
            return Task.FromResult(!RestoreDeclined);
        }

        public IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now) =>
        [
            .. IsPanReady ? Array.Empty<HealthIssue>() : [new("pan-missing", HealthSeverity.Warning, "Wächterdienst Pan fehlt", "Test", HealthFix.InstallDrivers, "Installieren")],
            .. ControllerSupportPlatform.CheckHidHide(HidHideVersion) is { } hidHide ? [hidHide] : Array.Empty<HealthIssue>(),
        ];
    }
}
