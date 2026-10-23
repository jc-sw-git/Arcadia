using Arcadia.Core.Diagnostics;

namespace Arcadia.Core.Tests;

public class LogPrivacyTests
{
    private const string Profile = @"C:\Users\Juri Name";

    [Theory]
    [InlineData(@"Spiel in C:\Users\Juri Name\Games\Hollow beendet", @"Spiel in %USERPROFILE%\Games\Hollow beendet")]
    [InlineData(@"at Foo() in c:\users\juri name\src\Foo.cs:line 3", @"at Foo() in %USERPROFILE%\src\Foo.cs:line 3")]
    [InlineData("Ordner c:/users/juri name/AppData/Local", "Ordner %USERPROFILE%/AppData/Local")]
    [InlineData(@"D:\Games\Juri Name\Hollow", @"D:\Games\Juri Name\Hollow")]
    [InlineData("Gamepad: A → Accept", "Gamepad: A → Accept")]
    public void Paths_in_the_users_profile_lose_the_users_name(string message, string expected)
    {
        Assert.Equal(expected, LogPrivacy.WithoutUserProfile(message, Profile));
    }

    [Fact]
    public void A_profile_given_with_a_trailing_separator_is_found_too()
    {
        Assert.Equal(@"%USERPROFILE%\x", LogPrivacy.WithoutUserProfile(@"C:\Users\Juri Name\x", Profile + @"\"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\")]
    public void Without_a_real_profile_folder_nothing_changes(string? profile)
    {
        Assert.Equal(@"C:\Users\Juri Name\x", LogPrivacy.WithoutUserProfile(@"C:\Users\Juri Name\x", profile));
    }
}
