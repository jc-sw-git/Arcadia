namespace Arcadia.Pan;

/// <summary>
/// The HidHide installer "Treiber installieren" brings when HidHide is missing or its version unsupported (#243 part
/// B2): pinned to the version verified on the owner's PC and on the CI runner, so a new major (which Pan refuses) never
/// reaches a player. Arcadia checks the download against <see cref="Sha256"/>; Pan checks the signature of its own copy.
/// </summary>
public static class HidHideRelease
{
    public static Version Version { get; } = new(1, 5, 230);

    /// <summary>
    /// The file version of the <c>HidHide.sys</c> it installs, which is what Arcadia and Pan read: the driver keeps its
    /// own numbering (seen on the CI runner, 2026-10-27).
    /// </summary>
    public static Version DriverVersion { get; } = new(1, 4, 181);

    public static Uri InstallerUrl { get; } = new("https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe");

    public const string InstallerName = "HidHide_1.5.230_x64.exe";

    /// <summary>The same as winget's manifest of Nefarius.HidHide 1.5.230 and <c>tools/ci/Install-HidHide.ps1</c>.</summary>
    public const string Sha256 = "f4bbbcb82e6258641b887c74bc81c4c5f66e4aa811808dfc304347687b7605f6";

    public const long InstallerSize = 8_078_016;

    /// <summary>
    /// Checked 2026-10-27 on the 1.5.230 installer (DigiCert EV code signing, timestamped; the certificate expired in
    /// 2025, the timestamp keeps the signature valid). A new publisher needs a look and a change here.
    /// </summary>
    public static PublisherPin Publisher { get; } = new("Nefarius Software Solutions e.U.", "AT", "505039f");

    public const string ManualInstallUrl = "https://github.com/nefarius/HidHide/releases";

    /// <summary>
    /// An Advanced Installer bootstrapper: <c>/exenoui /qn</c> is its silent mode (Burn's <c>/install /quiet</c> opened
    /// its UI and waited on the CI runner).
    /// </summary>
    public static IReadOnlyList<string> SilentArguments { get; } = ["/exenoui", "/qn", "/norestart"];

    /// <summary>The versions Pan hides with (<see cref="SteamControllerHiding.MinimumVersion"/> up to the next major).</summary>
    public static bool IsSupported(Version version) =>
        version.Major == SteamControllerHiding.SupportedMajorVersion && version >= SteamControllerHiding.MinimumVersion;
}

/// <summary>How HidHide's silent setup ended, as Pan judges it.</summary>
public enum HidHideSetupOutcome
{
    Installed,

    /// <summary>Installed; Windows wants a restart (exit 3010) before every HID device has the filter.</summary>
    InstalledRestartPending,

    /// <summary>
    /// The setup still runs after Pan's wait, but the driver is in place: its last action (registering HidHide's
    /// updater) hangs on some PCs (seen on about half the CI runners). It is left running, killing it could roll the
    /// driver back.
    /// </summary>
    InstalledSetupStillRunning,

    Failed,
}

public static class HidHideSetup
{
    /// <summary>How long Pan waits for HidHide's setup; it takes seconds when it doesn't hang.</summary>
    public static TimeSpan Wait => PanTimeouts.HidHideInstaller;

    private const int RestartRequired = 3010;

    /// <param name="exitCode">The setup's exit code, or null if it still ran after <see cref="Wait"/>.</param>
    /// <param name="installedVersion">HidHide's driver version as Windows lists it now, or null.</param>
    public static HidHideSetupOutcome Judge(int? exitCode, Version? installedVersion) => exitCode switch
    {
        0 when installedVersion is { } v && HidHideRelease.IsSupported(v) => HidHideSetupOutcome.Installed,
        RestartRequired when installedVersion is { } v && HidHideRelease.IsSupported(v) => HidHideSetupOutcome.InstalledRestartPending,
        null when installedVersion is { } v && HidHideRelease.IsSupported(v) => HidHideSetupOutcome.InstalledSetupStillRunning,
        _ => HidHideSetupOutcome.Failed,
    };
}
