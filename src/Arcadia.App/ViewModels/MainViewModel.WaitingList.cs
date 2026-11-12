using Arcadia.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>The window's part of "Warten auf deine Auswahl" (#491): the waiting list over the settings, and back to its row.</summary>
public partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaitingListOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial WaitingListViewModel? WaitingList { get; private set; }

    public bool IsWaitingListOpen => WaitingList is not null;

    partial void OnWaitingListChanged(WaitingListViewModel? oldValue, WaitingListViewModel? newValue) => oldValue?.Dispose();

    void IDataSourcesHost.OpenWaitingList()
    {
        if (_artwork is not null)
        {
            WaitingList = new WaitingListViewModel(
                _artwork,
                new WaitingKeyboard(editor => SettingsKeyboard.Take(editor, Input.UsesController), SettingsKeyboard.GiveBack),
                CloseWaitingList);
        }
    }

    /// <summary>The text being written with the on-screen keyboard: the waiting list's own term or a text of the settings.</summary>
    public TextEditorViewModel? OpenTextEditor => WaitingList?.Hits?.Editor ?? Settings?.Selected.Editor;

    /// <summary>B or the ✕: the settings again, the focus on the row the list was opened from.</summary>
    private void CloseWaitingList(WaitingListViewModel list)
    {
        if (WaitingList != list)
        {
            return;
        }

        WaitingList = null;
        Settings?.Selected?.FocusRow(ArtworkNameRows.WaitingKey);
    }
}
