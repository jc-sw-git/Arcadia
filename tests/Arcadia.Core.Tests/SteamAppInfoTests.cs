using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public sealed class SteamAppInfoTests : IDisposable
{
    private static readonly (uint, string, string)[] Apps =
    [
        (220, "Half-Life 2", "Game"),
        (431960, "Wallpaper Engine", "Application"),
        (250820, "SteamVR", "Tool"),
        (1145350, "Hades II Soundtrack", "Music"),
        (999999, "Not installed", "Game"),
    ];

    private readonly string _path = Path.Combine(Path.GetTempPath(), "arcadia-appinfo-" + Guid.NewGuid().ToString("N") + ".vdf");

    public void Dispose() => File.Delete(_path);

    [Theory]
    [InlineData(AppInfoFile.V27)]
    [InlineData(AppInfoFile.V28)]
    [InlineData(AppInfoFile.V29)]
    public void Reads_the_type_of_the_asked_apps_only(uint version)
    {
        File.WriteAllBytes(_path, AppInfoFile.Build(version, Apps));

        var types = SteamAppInfo.ReadTypes(_path, new HashSet<uint> { 431960, 250820, 1145350, 220, 12345 });

        Assert.Equal(new Dictionary<uint, string> { [220] = "Game", [431960] = "Application", [250820] = "Tool", [1145350] = "Music" }, types);
    }

    [Fact]
    public void Rejects_an_unknown_version()
    {
        File.WriteAllBytes(_path, AppInfoFile.Build(0x07564430, Apps));

        Assert.Throws<FormatException>(() => SteamAppInfo.ReadTypes(_path, new HashSet<uint> { 220 }));
    }

    [Theory]
    [InlineData(AppInfoFile.V28)]
    [InlineData(AppInfoFile.V29)]
    public void Rejects_a_cut_off_file(uint version)
    {
        var bytes = AppInfoFile.Build(version, Apps);
        File.WriteAllBytes(_path, bytes[..(bytes.Length / 2)]);

        Assert.Throws<FormatException>(() => SteamAppInfo.ReadTypes(_path, new HashSet<uint> { 999999 }));
    }

    [Fact]
    public void Rejects_an_entry_larger_than_the_file()
    {
        var bytes = AppInfoFile.Build(AppInfoFile.V28, Apps);
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 12);
        File.WriteAllBytes(_path, bytes);

        Assert.Throws<FormatException>(() => SteamAppInfo.ReadTypes(_path, new HashSet<uint> { 999999 }));
    }

    [Fact]
    public void Rejects_a_key_outside_the_string_table()
    {
        var bytes = AppInfoFile.Build(AppInfoFile.V29, [(220, "Half-Life 2", "Game")]);

        // The first key of the first entry ("appinfo", index 0) after header (16), id and size (8) and the entry header (60).
        BitConverter.GetBytes(77u).CopyTo(bytes, 16 + 8 + 60 + 1);
        File.WriteAllBytes(_path, bytes);

        Assert.Throws<FormatException>(() => SteamAppInfo.ReadTypes(_path, new HashSet<uint> { 220 }));
    }

    [Fact]
    public void A_missing_file_is_an_IO_error()
    {
        Assert.Throws<FileNotFoundException>(() => SteamAppInfo.ReadTypes(_path, new HashSet<uint> { 220 }));
    }
}
