using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

/// <summary>Steam Input per game from localconfig.vdf, as SISR's pause for such games reads it (#124).</summary>
public sealed class SteamInputSettingTests
{
    private static string LocalConfig(string? gameSetting, string xboxSupport = "0", string genericSupport = "0") => $$"""
        "UserLocalConfigStore"
        {
        	"Software"
        	{
        		"Valve" { "Steam" { "apps" { "4231820" { "Playtime" "1100" } } } }
        	}
        	"apps"
        	{
        		"4231820"
        		{
        			{{(gameSetting is null ? "" : $"\"UseSteamControllerConfig\"\t\t\"{gameSetting}\"")}}
        			"SteamControllerRumble"		"-1"
        		}
        	}
        	"SteamController_XBoxSupport"		"{{xboxSupport}}"
        	"SteamController_GenericGamepadSupport"		"{{genericSupport}}"
        }
        """;

    [Fact]
    public void Steam_Input_on_for_every_controller_covers_other_controllers()
    {
        var setting = SteamInputSetting.Read(LocalConfig("2"), 4231820);

        Assert.NotNull(setting);
        Assert.True(setting.CoversOtherControllers);
        Assert.Contains("UseSteamControllerConfig 2", setting.Description);
    }

    [Fact]
    public void Steam_Input_off_for_the_game_covers_nothing_even_with_Xbox_support_on()
    {
        Assert.False(SteamInputSetting.Read(LocalConfig("0", xboxSupport: "1"), 4231820)?.CoversOtherControllers);
    }

    [Theory]
    [InlineData("1", "1", "0", true)]
    [InlineData(null, "0", "1", true)]
    [InlineData("1", "0", "0", null)]
    [InlineData(null, "0", "0", null)]
    [InlineData("7", "1", "1", null)]
    public void Steams_default_follows_the_global_Xbox_and_generic_support(string? gameSetting, string xbox, string generic, bool? covers)
    {
        Assert.Equal(covers, SteamInputSetting.Read(LocalConfig(gameSetting, xbox, generic), 4231820)?.CoversOtherControllers);
    }

    [Fact]
    public void An_app_without_an_entry_has_Steams_default()
    {
        var setting = SteamInputSetting.Read(LocalConfig("2", xboxSupport: "1"), 620);

        Assert.Null(setting?.GameSetting);
        Assert.True(setting?.CoversOtherControllers);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("\"UserLocalConfigStore\" {")]
    [InlineData("\"Other\" { }")]
    public void A_missing_or_broken_file_tells_nothing(string? text)
    {
        Assert.Null(SteamInputSetting.Read(text, 4231820));
    }
}
