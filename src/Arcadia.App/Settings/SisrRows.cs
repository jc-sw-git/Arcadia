using Arcadia.ControllerSupport;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "SISR" in the controller settings (#214, artboard T214Settings): "SISR entfernen" and the indented "SISR
/// behalten". Shown while SISR or its Steam marker is there and the player didn't choose to keep it.
/// </summary>
public sealed class SisrRows
{
    public const string Group = "SISR";
    public const string RemoveKey = "sisr-remove";
    public const string KeepKey = "sisr-keep";

    private readonly ControllerSupportService _support;

    /// <param name="remove">[Entfernen]: the settings ask about Steam first, if they need to.</param>
    /// <param name="keep">[Ausblenden]: hides the group; SISR stays as it is.</param>
    public SisrRows(ControllerSupportService support, Action remove, Action keep)
    {
        _support = support;
        Remove = new ActionRowViewModel(RemoveKey, AppTexts.SisrRemove, remove) { Group = Group, ActionText = AppTexts.Remove };
        Keep = new ActionRowViewModel(KeepKey, AppTexts.SisrKeep, keep)
        {
            Group = Group,
            Indent = 1,
            ActionText = AppTexts.Hide,
            Subtitle = AppTexts.SisrHideSubtitle,
            Info =
            [
                new InfoText(AppTexts.SisrHideInfo),
                new InfoText(AppTexts.SisrHideInfoWarns),
                new InfoFootnote(AppTexts.SisrHideInfoReturns),
            ],
        };
    }

    public ActionRowViewModel Remove { get; }

    public ActionRowViewModel Keep { get; }

    /// <summary>The rows of the group, or none while it is hidden; call on every change of the support or the settings.</summary>
    public IReadOnlyList<SettingsRowViewModel> Build()
    {
        if (!_support.IsSupportedPlatform || !_support.Sisr.ShowsGroup || _support.Sisr.Found is not { } found)
        {
            return [];
        }

        var cause = _support.Sisr.RemovalCause;
        var busy = _support.BusyText is not null;
        Remove.Subtitle = cause is not null ? (_support.Sisr.RemovedNothing ? AppTexts.SisrNotRemoved : AppTexts.SisrNotAllRemoved)
            : found.Program ? AppTexts.SisrReplacedByPan
            : AppTexts.SisrSteamEntryLeft;
        Remove.Severity = cause is null ? null : HealthSeverity.Warning;
        Remove.IsEnabled = !busy;
        Keep.IsEnabled = !busy;
        Remove.Info =
        [
            new InfoText(AppTexts.SisrInfoPast),
            new InfoNote(AppTexts.SisrInfoNotBoth),
            .. cause is not null ? [new InfoNote(cause)] : Array.Empty<InfoBlock>(),
            // Empty when only something the cause names is left.
            .. Removed(found).Select(item => new InfoBullet(item)).ToList() is { Count: > 0 } removed
                ? [new InfoSection(AppTexts.SisrWhatIsRemoved, [.. removed])]
                : Array.Empty<InfoBlock>(),
            .. found.HasMarker
                ? [new InfoText(AppTexts.SisrSteamEntryInfo)]
                : Array.Empty<InfoBlock>(),
            new InfoSection(AppTexts.SisrWhatStays, [new InfoText(AppTexts.SisrKeeps)]),
            new InfoFootnote(AppTexts.SisrDisappearsWhenRemoved),
        ];
        return [Remove, Keep];
    }

    private static IEnumerable<string> Removed(SisrFound found)
    {
        if (found.Program)
        {
            yield return AppTexts.SisrRemovedFiles;
        }

        if (found.HasMarker)
        {
            yield return AppTexts.SisrRemovedMarker;
        }

        if (found.CefFlag)
        {
            yield return AppTexts.SisrRemovedRemoteControl;
        }
    }
}
