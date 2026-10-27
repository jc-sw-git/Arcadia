using Arcadia.Core.Diagnostics;

namespace Arcadia.Core.Tests;

public class LogPrivacyTests
{
    private const string Profile = @"C:\Users\Jane Doe";

    [Theory]
    [InlineData(@"Spiel in C:\Users\Jane Doe\Games\Hollow beendet", @"Spiel in %USERPROFILE%\Games\Hollow beendet")]
    [InlineData(@"at Foo() in c:\users\jane doe\src\Foo.cs:line 3", @"at Foo() in %USERPROFILE%\src\Foo.cs:line 3")]
    [InlineData("Ordner c:/users/jane doe/AppData/Local", "Ordner %USERPROFILE%/AppData/Local")]
    [InlineData(@"D:\Games\Jane Doe\Hollow", @"D:\Games\Jane Doe\Hollow")]
    [InlineData("Gamepad: A → Accept", "Gamepad: A → Accept")]
    public void Paths_in_the_users_profile_lose_the_users_name(string message, string expected)
    {
        Assert.Equal(expected, LogPrivacy.WithoutUserProfile(message, Profile));
    }

    [Fact]
    public void A_profile_given_with_a_trailing_separator_is_found_too()
    {
        Assert.Equal(@"%USERPROFILE%\x", LogPrivacy.WithoutUserProfile(@"C:\Users\Jane Doe\x", Profile + @"\"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\")]
    public void Without_a_real_profile_folder_nothing_changes(string? profile)
    {
        Assert.Equal(@"C:\Users\Jane Doe\x", LogPrivacy.WithoutUserProfile(@"C:\Users\Jane Doe\x", profile));
    }
}
