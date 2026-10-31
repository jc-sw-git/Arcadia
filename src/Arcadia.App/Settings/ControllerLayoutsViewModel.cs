using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Navigation;
using Arcadia.Input;
using Arcadia.Pan;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>One row of "Controller zuordnen": a connected model and the layout games see it as (designs T238Modal).</summary>
public sealed partial class ControllerLayoutRowViewModel : ViewModelBase
{
    private readonly Action<ControllerLayoutRowViewModel, int> _step;
    private readonly Action<ControllerLayoutRowViewModel> _focus;

    public ControllerLayoutRowViewModel(string key, string title, string subtitle, bool isLocked, Action<ControllerLayoutRowViewModel, int> step, Action<ControllerLayoutRowViewModel> focus)
    {
        Key = key;
        Title = title;
        Subtitle = subtitle;
        IsLocked = isLocked;
        _step = step;
        _focus = focus;
    }

    public string Key { get; }

    public string Title { get; }

    /// <summary>Only the default, e.g. "Vorgabe: Xbox 360 Controller".</summary>
    public string Subtitle { get; }

    /// <summary>The Steam Controller: its layout in mode "Arcadia" can't be chosen yet (#336).</summary>
    public bool IsLocked { get; }

    public bool IsOpen => !IsLocked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotTakenOver))]
    public partial ControllerLayout Layout { get; set; }

    [ObservableProperty]
    public partial string ValueText { get; set; } = string.Empty;

    /// <summary>"Nicht übernehmen" is shown softer than a layout.</summary>
    public bool IsNotTakenOver => !Layout.IsTakenOver;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLockNote))]
    public partial bool IsFocused { get; set; }

    /// <summary>Why the Steam Controller is locked, shown while it is focused.</summary>
    public bool ShowsLockNote => IsLocked && IsFocused;

    [RelayCommand]
    private void StepBack() => _step(this, -1);

    [RelayCommand]
    private void StepOn() => _step(this, 1);

    /// <summary>A click on the row: the focus goes there.</summary>
    [RelayCommand]
    private void Point() => _focus(this);
}

/// <summary>
/// The modal "Controller zuordnen" (#238 D3a, designs T238Modal, flow T238F01–F04, F08, F09): one row per connected
/// controller model, ▲ ▼ moves the focus, ◀ ▶ cycles its layouts and "Nicht übernehmen" (wrapping) and applies at once,
/// B closes. The Steam Controller is listed locked to Xbox 360 (#336). Rows follow controllers coming and going while it
/// is open; the focus stays on its model.
/// </summary>
public sealed partial class ControllerLayoutsViewModel : ViewModelBase
{
    public const string Title = "Controller zuordnen";
    public const string Text = "Lege fest, welches Controller-Layout deine verbundenen Controller erhalten. Das Layout gilt pro Modell, nicht pro Controller: Alle DualSense Controller bekommen also dasselbe Layout.";
    public const string Hint = "Die Änderung gilt sofort. Läuft gerade ein Spiel, verbindet sich der Controller dort einmal neu.";
    public const string LockNote = "Der Arcadia-Modus für den Steam Controller ist bisher auf das Xbox 360 Layout begrenzt. Ein Update wird das ändern.";
    public const string SteamControllerTitle = "Steam Controller (Modus „Arcadia“)";
    public const string SteamControllerKey = "steam-controller";

    private readonly Func<IReadOnlyList<ConnectedModel>> _models;
    private readonly Func<bool> _steamControllerConnected;
    private readonly Func<ControllerModel, ControllerLayout?> _chosen;
    private readonly Action<ControllerModel, ControllerLayout?> _choose;
    private readonly Action _close;
    private readonly Dictionary<string, ConnectedModel> _byKey = [];
    private int _focus = -1;

    /// <param name="models">The models connected now, but the Steam Controller.</param>
    /// <param name="chosen">The stored choice of a model; null: its default.</param>
    /// <param name="choose">Stores a choice; null puts the model back on its default.</param>
    public ControllerLayoutsViewModel(Func<IReadOnlyList<ConnectedModel>> models, Func<bool> steamControllerConnected,
        Func<ControllerModel, ControllerLayout?> chosen, Action<ControllerModel, ControllerLayout?> choose, Action close)
    {
        _models = models;
        _steamControllerConnected = steamControllerConnected;
        _chosen = chosen;
        _choose = choose;
        _close = close;
        Refresh();
    }

    public ObservableCollection<ControllerLayoutRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChange))]
    public partial ControllerLayoutRowViewModel? FocusedRow { get; private set; }

    /// <summary>The footer names ◀ ▶ only where they change something.</summary>
    public bool CanChange => FocusedRow is { IsLocked: false };

    /// <summary>The focus moved: the view keeps the row in sight.</summary>
    public event EventHandler? FocusMoved;

    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Up:
                FocusAt(ListFocus.Move(_focus, -1, Rows.Count));
                break;
            case UiAction.Down:
                FocusAt(ListFocus.Move(_focus, 1, Rows.Count));
                break;
            case UiAction.Left when FocusedRow is { } row:
                Step(row, -1);
                break;
            case UiAction.Right when FocusedRow is { } row:
                Step(row, 1);
                break;
            case UiAction.Back:
                Close();
                break;
        }
    }

    /// <summary>
    /// Controllers came or went: rows of models still connected keep their instance and value, new ones are added in
    /// connection order, the focus stays on its model (or the row at its place).
    /// </summary>
    public void Refresh()
    {
        var focusedKey = FocusedRow?.Key;
        var previous = Rows.ToDictionary(row => row.Key);
        List<ControllerLayoutRowViewModel> rows = [];
        _byKey.Clear();
        if (_steamControllerConnected())
        {
            rows.Add(previous.GetValueOrDefault(SteamControllerKey) ?? new ControllerLayoutRowViewModel(SteamControllerKey, SteamControllerTitle,
                Default(PadKind.Xbox360), isLocked: true, Step, Focus) { Layout = ControllerLayout.Of(PadKind.Xbox360), ValueText = PadKinds.DisplayName(PadKind.Xbox360) });
        }

        foreach (var model in _models())
        {
            var key = ControllerLayouts.Key(model.Model);
            _byKey[key] = model;
            var row = previous.GetValueOrDefault(key) ?? new ControllerLayoutRowViewModel(key, model.Name, Default(model.DefaultKind), isLocked: false, Step, Focus);
            Show(row, Current(model));
            rows.Add(row);
        }

        if (!Rows.SequenceEqual(rows))
        {
            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(row);
            }
        }

        FocusAt(ListFocus.Keep(focusedKey, Math.Max(0, _focus), [.. Rows.Select(row => row.Key)]), force: true);
    }

    [RelayCommand]
    private void Close() => _close();

    private static string Default(PadKind kind) => $"Vorgabe: {PadKinds.DisplayName(kind)}";

    /// <summary>What the model is now: its stored choice where the model may be that, else its own kind.</summary>
    private ControllerLayout Current(ConnectedModel model) => ControllerLayouts.Effective(_chosen(model.Model), model.DefaultKind);

    private static void Show(ControllerLayoutRowViewModel row, ControllerLayout layout)
    {
        row.Layout = layout;
        row.ValueText = ControllerLayouts.DisplayName(layout);
    }

    private void Step(ControllerLayoutRowViewModel row, int delta)
    {
        Focus(row);
        if (row.IsLocked || !_byKey.TryGetValue(row.Key, out var model))
        {
            return;
        }

        var choices = ControllerLayouts.Choices(model.DefaultKind);
        var next = choices[ListFocus.Cycle(Math.Max(0, IndexOf(choices, row.Layout)), delta, choices.Count)];
        Show(row, next);

        // The default is stored as no choice: the model follows its own kind (D3b).
        _choose(model.Model, next == ControllerLayout.Of(model.DefaultKind) ? null : next);
    }

    private static int IndexOf(IReadOnlyList<ControllerLayout> choices, ControllerLayout layout)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (choices[i] == layout)
            {
                return i;
            }
        }

        return -1;
    }

    private void Focus(ControllerLayoutRowViewModel row) => FocusAt(Rows.IndexOf(row));

    private void FocusAt(int index, bool force = false)
    {
        var row = index >= 0 && index < Rows.Count ? Rows[index] : null;
        if (!force && index == _focus && row == FocusedRow)
        {
            return;
        }

        if (FocusedRow is { } previous && previous != row)
        {
            previous.IsFocused = false;
        }

        _focus = index;
        FocusedRow = row;
        if (row is not null)
        {
            row.IsFocused = true;
        }

        FocusMoved?.Invoke(this, EventArgs.Empty);
    }
}
