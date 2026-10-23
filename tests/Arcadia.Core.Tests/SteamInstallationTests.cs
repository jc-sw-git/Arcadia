using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public class SteamInstallationTests : IDisposable
{
    private const ulong SteamId64Base = 76561197960265728;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-tests-" + Guid.NewGuid());

    public SteamInstallationTests() => Directory.CreateDirectory(Path.Combine(_root, "config"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteLoginUsers(string body) =>
        File.WriteAllText(Path.Combine(_root, "config", "loginusers.vdf"), "\"users\"\n{\n" + body + "}\n");

    private static string User(uint accountId, string extra) =>
        $"\t\"{SteamId64Base + accountId}\"\n\t{{\n\t\t\"AccountName\"\t\t\"a{accountId}\"\n{extra}\t}}\n";

    private void CreateAccount(uint id) => Directory.CreateDirectory(Path.Combine(_root, "userdata", id.ToString()));

    [Fact]
    public void With_several_accounts_and_Steam_closed_the_most_recent_one_is_used()
    {
        CreateAccount(111);
        CreateAccount(222);
        WriteLoginUsers(User(111, "\t\t\"timestamp\"\t\t\"500\"\n") + User(222, "\t\t\"MostRecent\"\t\t\"1\"\n\t\t\"timestamp\"\t\t\"100\"\n"));

        var steam = new SteamInstallation(_root, activeUserId: 0);

        Assert.Equal(222u, steam.LastLoggedInUserId());
        Assert.Equal(Path.Combine(_root, "userdata", "222"), steam.UserDataDirectory);
    }

    [Fact]
    public void Without_MostRecent_the_newest_timestamp_wins()
    {
        CreateAccount(111);
        CreateAccount(222);
        WriteLoginUsers(User(111, "\t\t\"timestamp\"\t\t\"500\"\n") + User(222, "\t\t\"timestamp\"\t\t\"100\"\n"));

        Assert.Equal(Path.Combine(_root, "userdata", "111"), new SteamInstallation(_root, activeUserId: null).UserDataDirectory);
    }

    [Fact]
    public void The_logged_in_account_beats_the_login_history()
    {
        CreateAccount(111);
        CreateAccount(222);
        WriteLoginUsers(User(111, "\t\t\"MostRecent\"\t\t\"1\"\n"));

        Assert.Equal(Path.Combine(_root, "userdata", "222"), new SteamInstallation(_root, activeUserId: 222).UserDataDirectory);
    }

    [Fact]
    public void Several_accounts_without_a_login_history_give_no_folder()
    {
        CreateAccount(111);
        CreateAccount(222);

        Assert.Null(new SteamInstallation(_root, activeUserId: 0).UserDataDirectory);
    }

    [Fact]
    public void Accounts_are_named_after_their_profile_and_the_last_one_comes_first()
    {
        CreateAccount(111);
        CreateAccount(222);
        CreateAccount(333);
        Directory.CreateDirectory(Path.Combine(_root, "userdata", "0"));
        WriteLoginUsers(User(111, "\t\t\"PersonaName\"\t\t\"Zora\"\n")
                        + User(222, "\t\t\"PersonaName\"\t\t\"Anna\"\n\t\t\"MostRecent\"\t\t\"1\"\n"));

        var accounts = new SteamInstallation(_root, activeUserId: null).Accounts();

        // 333 was removed from Steam's login list; its folder stays but it can't log in any more.
        Assert.Equal([new SteamAccount(222, "Anna"), new SteamAccount(111, "Zora")], accounts);
    }

    [Fact]
    public void Without_a_login_list_the_userdata_folders_are_the_accounts()
    {
        CreateAccount(111);
        CreateAccount(222);
        Directory.CreateDirectory(Path.Combine(_root, "userdata", "0"));

        var accounts = new SteamInstallation(_root, activeUserId: null).Accounts();

        Assert.Equal([new SteamAccount(111, "111"), new SteamAccount(222, "222")], accounts);
    }

    [Fact]
    public void Without_userdata_there_are_no_accounts() =>
        Assert.Empty(new SteamInstallation(_root, activeUserId: null).Accounts());

    [Fact]
    public void A_folder_picked_by_hand_is_Steam_if_it_holds_steam_exe_and_steamapps()
    {
        Directory.CreateDirectory(Path.Combine(_root, "steamapps"));
        File.WriteAllText(Path.Combine(_root, "steam.exe"), string.Empty);

        Assert.Equal(_root, SteamInstallation.RootIn(_root));
        Assert.Equal(_root, SteamInstallation.RootIn(_root + Path.DirectorySeparatorChar));
        Assert.Equal(_root, SteamInstallation.RootIn(Path.Combine(_root, "steamapps")));
    }

    [Fact]
    public void A_folder_without_steam_exe_or_steamapps_or_a_relative_one_is_no_Steam()
    {
        Directory.CreateDirectory(Path.Combine(_root, "steamapps"));

        Assert.Null(SteamInstallation.RootIn(_root));
        File.WriteAllText(Path.Combine(_root, "steam.exe"), string.Empty);
        Assert.Null(SteamInstallation.RootIn(Path.Combine(_root, "config")));
        Assert.Null(SteamInstallation.RootIn("Steam"));
        Assert.Null(SteamInstallation.RootIn(string.Empty));
    }
}
