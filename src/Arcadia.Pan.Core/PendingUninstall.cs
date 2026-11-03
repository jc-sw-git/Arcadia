using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arcadia.Pan;

/// <summary>
/// An uninstall that is not finished yet (#422): written before <c>--uninstall</c> changes anything, it says whether
/// the player chose to remove the drivers and how often Pan tried to. While it exists, Pan's service finishes the
/// uninstall at its start; a new <c>--install</c> deletes it, as the player wants Arcadia again.
/// </summary>
public sealed record PendingUninstall(bool RemoveDrivers, int DriverAttempts);

/// <summary>Where <see cref="PendingUninstall"/> is kept; a fake in tests.</summary>
public interface IPendingUninstallStore
{
    /// <summary>Null when no uninstall is pending.</summary>
    PendingUninstall? Load();

    void Save(PendingUninstall pending);

    /// <summary>True when there was one to delete.</summary>
    bool Delete();
}

[JsonSerializable(typeof(PendingUninstall))]
internal sealed partial class PendingUninstallJson : JsonSerializerContext;

/// <summary>
/// The marker file in Pan's data folder, which only administrators and SYSTEM can write. One that can't be read still
/// means "uninstall pending", but never that the drivers go: the player's yes to them is not known any more.
/// </summary>
public sealed class PendingUninstallFile(string path) : IPendingUninstallStore
{
    public const string FileName = "uninstall-pending.json";

    public PendingUninstall? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var pending = JsonSerializer.Deserialize(stream, PendingUninstallJson.Default.PendingUninstall);
            return pending is { DriverAttempts: >= 0 } ? pending : Unreadable;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Unreadable;
        }
    }

    public void Save(PendingUninstall pending)
    {
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, pending, PendingUninstallJson.Default.PendingUninstall);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    public bool Delete()
    {
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private static readonly PendingUninstall Unreadable = new(RemoveDrivers: false, DriverAttempts: 0);
}
