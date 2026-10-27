using Arcadia.Launchers;
using Arcadia.Launchers.Steam;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>The Steam sign-in (#83) is off until it is reworked: the owner's account was restricted after it (2026-10-27).</summary>
public sealed class SteamSignInOffTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-signin-off-").FullName;

    [Fact]
    public void The_app_gets_no_Steam_account_and_a_stored_sign_in_is_deleted()
    {
        File.WriteAllText(Path.Combine(_folder, "steam-account.dat"), "stored by an earlier build");
        File.WriteAllText(Path.Combine(_folder, "steam-owned-games.json"), "{}");
        using var http = new HttpClient();

        var steam = BuiltInLaunchers.Create(new FakeTimeProvider(), demo: false, http: http, dataFolder: _folder).OfType<SteamIntegration>().Single();

        Assert.Null(steam.Account);
        Assert.False(File.Exists(Path.Combine(_folder, "steam-account.dat")));
        Assert.False(File.Exists(Path.Combine(_folder, "steam-owned-games.json")));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
