using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Status;

/// <summary>One entry of the status modal (#166): level symbol, title, short text, "seit hh:mm" and its buttons.</summary>
public sealed partial class StatusEntryViewModel : ViewModelBase
{
    private readonly Action<StatusEntryViewModel, bool> _press;

    /// <param name="press">A button was clicked: true for the fix, false for "Zu den Einstellungen".</param>
    public StatusEntryViewModel(StatusEntry entry, Action<StatusEntryViewModel, bool> press)
    {
        _press = press;
        Entry = entry;
    }

    public const string SettingsLabel = "Zu den Einstellungen";

    /// <summary>The message as the header shows it now; replaced when its text or time changes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(ShortText), nameof(SinceText), nameof(FixLabel), nameof(HasFix), nameof(IsError), nameof(IsWarning),
        nameof(IsUpdate), nameof(IsInfo))]
    public partial StatusEntry Entry { get; set; }

    public string Id => Entry.Id;

    public string Title => Entry.Title;

    public string ShortText => Entry.ShortText;

    public string SinceText => $"seit {Formatting.Clock(Entry.Since)}";

    public string FixLabel => Entry.FixLabel ?? string.Empty;

    public bool HasFix => Entry.FixLabel is not null;

    public bool IsError => Entry.Level == StatusLevel.Error;

    public bool IsWarning => Entry.Level == StatusLevel.Warning;

    public bool IsUpdate => Entry.Level == StatusLevel.Update;

    public bool IsInfo => Entry.Level == StatusLevel.Info;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFixFocused), nameof(IsSettingsFocused))]
    public partial bool IsFocused { get; set; }

    /// <summary>◀ ▶ chose the fix; otherwise "Zu den Einstellungen" is the focused button.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFixFocused), nameof(IsSettingsFocused), nameof(FocusedLabel))]
    public partial bool FixChosen { get; set; }

    public bool IsFixFocused => IsFocused && FixChosen && HasFix;

    public bool IsSettingsFocused => IsFocused && !IsFixFocused;

    /// <summary>What A does now, for the footer.</summary>
    public string FocusedLabel => FixChosen && HasFix ? FixLabel : SettingsLabel;

    [RelayCommand]
    private void Fix() => _press(this, true);

    [RelayCommand]
    private void OpenSettings() => _press(this, false);
}
