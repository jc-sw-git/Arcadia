namespace Arcadia.Pan.Tests;

/// <summary>Who may do what in Pan's data folder (owner, 2026-10-27: users read the log, nobody else writes).</summary>
public sealed class DataFolderRightsTests
{
    [Theory]
    [Trait("TestCase", "T243-45")]
    [InlineData(0x1200A9u, false)] // ReadAndExecute: what users get, to read pan.log
    [InlineData(0x120089u, false)] // Read
    [InlineData(0x80000000u, false)] // GENERIC_READ
    [InlineData(0x1F01FFu, true)] // FullControl: what Explorer's "Fortsetzen" gave the owner's user
    [InlineData(0x1301BFu, true)] // Modify
    [InlineData(0x2u, true)] // WriteData / CreateFiles
    [InlineData(0x4u, true)] // AppendData / CreateDirectories
    [InlineData(0x40u, true)] // DeleteSubdirectoriesAndFiles
    [InlineData(0x10000u, true)] // Delete
    [InlineData(0x40000u, true)] // ChangePermissions
    [InlineData(0x80000u, true)] // TakeOwnership
    [InlineData(0x40000000u, true)] // GENERIC_WRITE in an inherit-only rule
    [InlineData(0x10000000u, true)] // GENERIC_ALL
    public void Only_rights_that_change_the_folder_make_it_untrusted(uint mask, bool writes)
    {
        Assert.Equal(writes, DataFolderRights.Writes(mask));
    }

    [Theory]
    [InlineData("--install", true)]
    [InlineData("--uninstall", true)]
    [InlineData("--restore", true)]
    [InlineData("--console", true)]
    [InlineData("--version", false)]
    [InlineData("", false)]
    public void Commands_that_change_the_machine_need_administrator_rights(string command, bool needs)
    {
        Assert.Equal(needs, PanCommands.NeedsAdministrator(command));
    }
}
