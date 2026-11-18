using Arcadia.App.Input;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>#683: "Artwork ändern" without internet offers what is on this PC and warns once, asking SteamGridDB nothing.</summary>
public sealed partial class ChangeArtworkTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T683-10")]
    public async Task Without_internet_the_modal_offers_the_images_on_this_pc_and_warns_once()
    {
        var alpha = Installed("Alpha");
        using var images = new LauncherImages();
        images.Set(alpha, ArtworkKind.Cover, TestImages.Png(600, 900));
        images.Set(alpha, ArtworkKind.Hero, TestImages.Png(1550, 500));
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 1, Library: [alpha], Artwork: images)]);
        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        await SettledAsync(arcadia);
        var asked = arcadia.SteamGridDb.Count;
        arcadia.Connection.Online = false;

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var modal = await OpenFromDetailPageAsync(arcadia);
        arcadia.Press(UiAction.NextTab);
        await SettledAsync(arcadia, modal);

        // Own file and the launcher's image, both on this PC; one warning for both tabs, SteamGridDB not asked.
        Assert.True(modal.IsHeroTab);
        Assert.Equal(2, modal.Tiles.Count);
        Assert.True(modal.Tiles[1].IsInUse);
        Assert.Equal(1, arcadia.Main.Toast.History.Entries.Count(e => e.Text == AppTexts.NoInternetConnection && e.Level == HealthSeverity.Warning));
        Assert.Equal(asked, arcadia.SteamGridDb.Count);
    }
}
