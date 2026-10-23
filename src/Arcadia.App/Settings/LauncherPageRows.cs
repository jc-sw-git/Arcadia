using Arcadia.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// A text the user writes, e.g. a launcher's name or keyword (#137): A or a click open the on-screen keyboard, and the
/// row shows the text being written with its caret until it is taken or dropped.
/// </summary>
public sealed partial class TextRowViewModel(string key, string title, Func<string> get, Action<TextRowViewModel> edit)
    : SettingsRowViewModel(key, title)
{
    /// <summary>Shown before the text and not part of it, e.g. the "/" of a keyword.</summary>
    public string Prefix { get; init; } = string.Empty;

    public bool HasPrefix => Prefix.Length > 0;

    public string ValueText => get();

    /// <summary>The text being written; null while the row only shows its value.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    public partial TextEditorViewModel? Editor { get; set; }

    public bool IsEditing => Editor is not null;

    public override bool CanActivate => true;

    public override string ActivateText => "Bearbeiten";

    public override void Activate() => edit(this);

    partial void OnEditorChanged(TextEditorViewModel? oldValue, TextEditorViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnEditorPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnEditorPropertyChanged;
        }

        Problem = newValue?.Problem;
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextEditorViewModel.Problem))
        {
            Problem = Editor?.Problem;
        }
    }

    /// <summary>The value changed: it is read again.</summary>
    public void Refresh() => OnPropertyChanged(nameof(ValueText));
}

/// <summary>
/// "Aktiv" on a launcher's page (#137): as in the list, a launcher that isn't installed can't be switched on, but one that
/// is active and no longer found can be switched off.
/// </summary>
public sealed partial class LauncherActiveRowViewModel(Func<bool> isActive, Action<bool> switchTo, Func<bool> isFound)
    : SwitchRowViewModel("launcher-active", "Aktiv", isActive, switchTo)
{
    public override bool CanIncrease => base.CanIncrease && isFound();
}

/// <summary>
/// The head of a launcher's page (#137): its logo, name and state, and beside it the launchers before and after it in the
/// list, which LT/RT (or a click on them) turn to. "Launcher" above the name goes back to the list, like B.
/// </summary>
public sealed partial class LauncherPageHeadViewModel(Action<int> turn, Action back) : ObservableObject
{
    [ObservableProperty]
    public partial LauncherBadge? Badge { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNeighbours))]
    public partial LauncherNeighbour? Previous { get; set; }

    [ObservableProperty]
    public partial LauncherNeighbour? Next { get; set; }

    /// <summary>One launcher alone has nothing to turn to.</summary>
    public bool HasNeighbours => Previous is not null;

    [RelayCommand]
    private void TurnBack() => turn(-1);

    [RelayCommand]
    private void TurnOn() => turn(1);

    [RelayCommand]
    private void Back() => back();
}

/// <summary>A launcher beside the shown one on its page.</summary>
public sealed record LauncherNeighbour(string Name, LauncherBadge Badge);
