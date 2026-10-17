using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public class SteamLoginLogTests : IDisposable
{
    private const string CompleteLogin = """
        [2026-10-16 21:46:27] [ None ] Starting login
        [2026-10-16 21:46:28] [ WaitingForServerResponse ] SetLoginState: WaitingForLibraryReady - OK
        [2026-10-16 21:46:29] [ WaitingForLibraryReady ] Received post-logon callback: L=0,P=0,A=0,R=0,C=0,I=0,CM=0
        [2026-10-16 21:46:29] [ WaitingForLibraryReady ] SetLoginState: Success - OK

        """;

    // Seen on the owner's PC (2026-10-16): the server accepted the login, the client never finished it.
    private const string HangingLogin = """
        [2026-10-16 21:46:46] [ None ] Starting login
        [2026-10-16 21:46:47] [ WaitingForServerResponse ] Received logon success response
        [2026-10-16 21:46:47] [ WaitingForServerResponse ] SetLoginState: WaitingForLibraryReady - OK
        [2026-10-16 21:46:48] [ WaitingForLibraryReady ] Received post-logon callback: L=0,P=0,A=0,R=1,C=0,I=0,CM=0

        """;

    private readonly string _path = Path.Combine(Path.GetTempPath(), "arcadia-tests-" + Guid.NewGuid() + ".txt");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void A_login_that_reached_success_is_complete() =>
        Assert.True(SteamLoginLog.IsLatestLoginCompleteIn(HangingLogin + CompleteLogin));

    [Fact]
    public void A_later_login_without_success_is_not_complete_even_after_a_successful_one() =>
        Assert.False(SteamLoginLog.IsLatestLoginCompleteIn(CompleteLogin + HangingLogin));

    [Fact]
    public void A_log_without_any_login_tells_nothing() =>
        Assert.Null(SteamLoginLog.IsLatestLoginCompleteIn("[2026-10-16 21:46:27] Client version: 1792110651\n"));

    [Fact]
    public void A_missing_log_tells_nothing() =>
        Assert.Null(SteamLoginLog.IsLatestLoginComplete(_path));

    [Fact]
    public void Only_the_end_of_a_long_log_is_read()
    {
        var oldLogins = string.Concat(Enumerable.Repeat(CompleteLogin, 500));
        File.WriteAllText(_path, oldLogins + HangingLogin);

        Assert.False(SteamLoginLog.IsLatestLoginComplete(_path));
    }
}
