using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

public sealed class GameFoldersTests
{
    [Fact]
    public void A_program_inside_a_game_folder_is_that_game()
    {
        var folders = GameFolders.Of([Game(@"F:\SteamLibrary\steamapps\common\Hades\")]);

        Assert.True(folders.Contain(@"F:\SteamLibrary\steamapps\common\Hades\x64\Hades.exe"));
        Assert.True(folders.Contain(@"f:\steamlibrary\STEAMAPPS\common\hades\Hades.exe"));
    }

    [Fact]
    public void A_folder_that_only_starts_with_the_same_name_is_another_game()
    {
        var folders = GameFolders.Of([Game(@"C:\Games\Hades")]);

        Assert.False(folders.Contain(@"C:\Games\Hades II\Hades2.exe"));
        Assert.False(folders.Contain(@"C:\Games\Hades"));
    }

    [Fact]
    public void Games_that_are_not_installed_or_have_no_folder_count_for_nothing()
    {
        var folders = GameFolders.Of([
            Game(@"C:\Games\Old") with { IsInstalled = false },
            Game(null),
            Game("  "),
        ]);

        Assert.False(folders.Contain(@"C:\Games\Old\old.exe"));
        Assert.False(folders.Contain(null));
    }

    [Fact]
    public void A_drive_root_as_install_folder_does_not_make_every_program_a_game()
    {
        var folders = GameFolders.Of([Game(@"D:\")]);

        Assert.False(folders.Contain(@"D:\Tools\editor.exe"));
    }

    [Fact]
    public void Empty_contains_nothing()
    {
        Assert.False(GameFolders.Empty.Contain(@"C:\Windows\explorer.exe"));
    }

    private static Game Game(string? installDirectory) => new()
    {
        Id = installDirectory ?? "none",
        LauncherId = "test",
        Title = "Game",
        Launch = new LaunchTarget.None(),
        InstallDirectory = installDirectory,
    };
}
