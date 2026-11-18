using Avalonia.Controls;

namespace Arcadia.App.Views;

/// <summary>"Titel ändern" (#496).</summary>
public partial class ChangeTitleView : UserControl
{
    public ChangeTitleView()
    {
        InitializeComponent();
    }

    /// <summary>Lets the shut drawer into the layout, which builds its keyboard; called after the window's first frame (#637).</summary>
    public void ShowShutDrawer() => KeyboardDrawer.Classes.Remove("afterFirstFrame");
}
