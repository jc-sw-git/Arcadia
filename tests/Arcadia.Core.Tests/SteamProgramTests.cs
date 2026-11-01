using Arcadia.Launchers.Steam;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>#373: Steam the program, as the controller support and SISR's cleanup ask it; safe without Steam.</summary>
public sealed class SteamProgramTests : IDisposable
{
    private const string Exe = @"C:\Tools\Made-up\tool.exe";
    private const uint Account = 100_000_002;
    private const uint OtherAccount = 100_000_003;

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-steam-program-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string ShortcutsOf(uint account) => Path.Combine(_root, "userdata", account.ToString(System.Globalization.CultureInfo.InvariantCulture), "config", "shortcuts.vdf");

    [Fact]
    public async Task Without_Steam_nothing_runs_nothing_is_found_and_closing_is_done_at_once()
    {
        var steam = new SteamProgram(() => null, () => false);

        Assert.Null(steam.Folder);
        Assert.False(steam.IsRunning);
        Assert.False(steam.IsReady);
        Assert.Equal(0, steam.ShortcutsTo(Exe));
        steam.RemoveShortcutsTo(Exe);
        steam.Start();
        Assert.True(await steam.CloseAsync(new FakeTimeProvider()));
    }

    [Fact]
    public void A_Steam_that_runs_isnt_ready_without_a_signed_in_user()
    {
        var steam = new SteamProgram(() => new SteamInstallation(_root, activeUserId: null), () => true);

        Assert.Equal(_root, steam.Folder);
        Assert.True(steam.IsRunning);
        Assert.False(steam.IsReady);
    }

    [Fact]
    public void Counts_and_removes_the_entries_to_a_program_in_every_account()
    {
        SteamShortcuts.Add(ShortcutsOf(Account), "Tool", Exe);
        SteamShortcuts.Add(ShortcutsOf(OtherAccount), "Tool", Exe);
        SteamShortcuts.Add(ShortcutsOf(OtherAccount), "Other", @"C:\Tools\Made-up\other.exe");
        var steam = new SteamProgram(() => new SteamInstallation(_root, Account), () => false);

        Assert.Equal(2, steam.ShortcutsTo(Exe));

        steam.RemoveShortcutsTo(Exe);

        Assert.Equal(0, steam.ShortcutsTo(Exe));
        Assert.Single(SteamShortcuts.Read(ShortcutsOf(OtherAccount)));
    }

    [Fact]
    public void An_unreadable_list_counts_as_none()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutsOf(Account))!);
        File.WriteAllBytes(ShortcutsOf(Account), [0x00, 0x01, 0x02]);
        var steam = new SteamProgram(() => new SteamInstallation(_root, Account), () => false);

        Assert.Equal(0, steam.ShortcutsTo(Exe));
    }
}
