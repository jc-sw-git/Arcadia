using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public class TextVdfTests
{
    [Fact]
    public void Parses_nested_blocks_and_values()
    {
        var root = TextVdf.Parse("""
            "AppState"
            {
            	"appid"		"1030300"
            	"UserConfig"
            	{
            		"language"		"english"
            	}
            }
            """);

        var app = root.GetMap("AppState");
        Assert.NotNull(app);
        Assert.Equal("1030300", app.GetString("appid"));
        Assert.Equal("english", app.GetMap("UserConfig")?.GetString("language"));
    }

    [Fact]
    public void Unescapes_quotes_and_backslashes()
    {
        var root = TextVdf.Parse("""
            "path"	"C:\\Program Files (x86)\\Steam"
            "name"	"The \"Quoted\" Game"
            "lines"	"a\nb\tc"
            """);

        Assert.Equal(@"C:\Program Files (x86)\Steam", root.GetString("path"));
        Assert.Equal("The \"Quoted\" Game", root.GetString("name"));
        Assert.Equal("a\nb\tc", root.GetString("lines"));
    }

    [Fact]
    public void Skips_comments_and_conditions_and_reads_unquoted_tokens()
    {
        var root = TextVdf.Parse("""
            // a comment
            settings
            {
            	"width" 1920 [$WIN32] // trailing comment
            	"empty" { }
            }
            """);

        var settings = root.GetMap("settings");
        Assert.NotNull(settings);
        Assert.Equal("1920", settings.GetString("width"));
        Assert.Empty(settings.GetMap("empty")!);
    }

    [Fact]
    public void Looks_up_keys_ignoring_case()
    {
        var root = TextVdf.Parse("\"Software\" { \"valve\" { \"Steam\" \"1\" } }");

        Assert.Equal("1", root.GetMap("software")?.GetMap("Valve")?.GetString("steam"));
    }

    [Theory]
    [InlineData("\"a\" { \"b\" \"c\"")]
    [InlineData("\"a\" \"b\" }")]
    [InlineData("\"a\"")]
    [InlineData("\"a\" \"unterminated")]
    [InlineData("{ }")]
    public void Rejects_malformed_text(string text)
    {
        Assert.Throws<FormatException>(() => TextVdf.Parse(text));
    }

    [Fact]
    public void Empty_text_is_an_empty_root()
    {
        Assert.Empty(TextVdf.Parse("  \n"));
    }
}
