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
    public void Forward_slashes_and_doubled_separators_from_launcher_data_still_match()
    {
        var folders = GameFolders.Of([Game("D:/Games/Epic//Hades/"), Game(@"E:\Games\.\Celeste")]);

        Assert.True(folders.Contain(@"D:\Games\Epic\Hades\Hades.exe"));
        Assert.True(folders.Contain(@"E:\Games\Celeste\Celeste.exe"));
        Assert.True(folders.Contain("E:/Games/Celeste/Celeste.exe"));
    }

    [Fact]
    public void Parent_folder_steps_are_resolved_before_comparing()
    {
        var folders = GameFolders.Of([Game(@"C:\Games\Launcher\..\Hades")]);

        Assert.True(folders.Contain(@"C:\Games\Hades\Hades.exe"));
        Assert.False(folders.Contain(@"C:\Games\Launcher\launcher.exe"));
        Assert.False(folders.Contain(@"C:\Games\Hades\..\Other\other.exe"));
    }

    [Fact]
    public void A_long_path_prefix_does_not_hide_the_game()
    {
        var folders = GameFolders.Of([Game(@"C:\Games\Hades")]);

        Assert.True(folders.Contain(@"\\?\C:\Games\Hades\Hades.exe"));
    }

    [Fact]
    public void It_names_the_game_whose_folder_holds_the_program()
    {
        var folders = GameFolders.Of([Game(@"C:\Games\Hades"), Game(@"C:\Games\Celeste")]);

        Assert.Equal(@"test:C:\Games\Celeste", folders.GameOf(@"C:\Games\Celeste\Celeste.exe"));
        Assert.Null(folders.GameOf(@"C:\Windows\explorer.exe"));
        Assert.Null(folders.GameOf(null));
    }

    [Fact]
    public void Of_two_folders_inside_each_other_the_inner_one_names_the_game()
    {
        var folders = GameFolders.Of([Game(@"C:\Games"), Game(@"C:\Games\Hades")]);

        Assert.Equal(@"test:C:\Games\Hades", folders.GameOf(@"C:\Games\Hades\Hades.exe"));
        Assert.Equal(@"test:C:\Games", folders.GameOf(@"C:\Games\Other\other.exe"));
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
