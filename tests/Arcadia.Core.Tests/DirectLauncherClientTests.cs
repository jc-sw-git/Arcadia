using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

public sealed class DirectLauncherClientTests
{
    [Theory]
    [InlineData("steam://rungameid/620")]
    [InlineData("com.epicgames.launcher://apps/ns%3Aitem%3AExampleGame?action=launch&silent=true")]
    public void A_launcher_protocol_is_started(string uri) =>
        Assert.True(DirectLauncherClient.IsStartable(new LaunchTarget.ProtocolUri(uri)));

    [Theory]
    [InlineData(@"C:\Games\example.exe")]
    [InlineData("file:///C:/Games/example.exe")]
    [InlineData(@"\\server\share\example.exe")]
    [InlineData("rungameid/620")]
    [InlineData("")]
    public void A_file_a_share_or_no_absolute_uri_is_not_started(string uri) =>
        Assert.False(DirectLauncherClient.IsStartable(new LaunchTarget.ProtocolUri(uri)));

    [Theory]
    [InlineData("Example.Game_8wekyb3d8bbwe!App", true)]
    [InlineData("Example.Game_8wekyb3d8bbwe!App\" /select,C:\\Windows", false)]
    [InlineData("Example Game!App", false)]
    [InlineData("", false)]
    public void An_app_id_has_only_the_characters_Windows_allows(string id, bool startable) =>
        Assert.Equal(startable, DirectLauncherClient.IsStartable(new LaunchTarget.ShellAppsFolder(id)));
}
