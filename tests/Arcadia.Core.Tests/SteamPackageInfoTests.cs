using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public sealed class SteamPackageInfoTests
{
    [Theory]
    [InlineData(PackageInfoFile.V39)]
    [InlineData(PackageInfoFile.V40)]
    public void Reads_the_apps_of_the_asked_packages_only(uint version)
    {
        var file = PackageInfoFile.Build(version, [(0, [7, 205]), (294, [6910]), (500, [10, 20])]);

        var packages = SteamPackageInfo.ReadApps(file, new HashSet<uint> { 0, 500, 999 });

        Assert.Equal([0u, 500u], packages.Keys.Order());
        Assert.Equal([7u, 205u], packages[0]);
        Assert.Equal([10u, 20u], packages[500]);
    }

    [Fact]
    public void An_unknown_version_is_a_format_error()
    {
        var file = PackageInfoFile.Build(0x06565526, [(0, [7])]);

        Assert.Throws<FormatException>(() => SteamPackageInfo.ReadApps(file, new HashSet<uint> { 0 }));
    }

    [Fact]
    public void A_cut_off_file_is_a_format_error()
    {
        var file = PackageInfoFile.Build(PackageInfoFile.V40, [(0, [7]), (500, [10])]);

        Assert.Throws<FormatException>(() => SteamPackageInfo.ReadApps(file.AsSpan(0, file.Length - 10).ToArray(), new HashSet<uint> { 500 }));
    }
}
