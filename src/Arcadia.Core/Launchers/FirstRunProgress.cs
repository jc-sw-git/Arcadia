using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arcadia.Core.Launchers;

/// <summary>The pages of the first start (#139, #497), in their order.</summary>
public enum FirstRunStep
{
    /// <summary>"Welche Launcher soll Arcadia nutzen?"</summary>
    Launchers,

    /// <summary>"Datenquellen": the SteamGridDB key and the IGDB access, skippable.</summary>
    DataSources,
}

/// <summary>
/// How far the first start got (#497): the page it was on, the launchers switched off on the launcher page, and the
/// launchers chosen there once it moved on to "Datenquellen". The keys aren't in it: each is saved by its own store the
/// moment it is taken.
/// </summary>
/// <param name="SwitchedOff">Found launchers the player switched off; one found later starts switched on.</param>
/// <param name="Chosen">The launchers "Fertig" chose, in tab order; null before.</param>
public sealed record FirstRunProgress(FirstRunStep Step, IReadOnlyList<string> SwitchedOff, IReadOnlyList<string>? Chosen = null)
{
    public static FirstRunProgress Start { get; } = new(FirstRunStep.Launchers, []);
}

/// <summary>
/// The record of <see cref="FirstRunProgress"/> beside <c>settings.json</c>, so the first start continues where it stopped,
/// also after a crash (#497). It is written to a temporary file and renamed, never half written. A record that is missing
/// or can't be read is no record: the first start begins again. Reading and writing never throw.
/// </summary>
public sealed class FirstRunProgressFile(string path)
{
    public const string FileName = "first-run.json";

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public string Path { get; } = path;

    /// <summary>Where a record that couldn't be read or written is said (arcadia.log); it never holds a key.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>The record in the folder of <paramref name="settingsPath"/>.</summary>
    public static FirstRunProgressFile Beside(string settingsPath, Action<string>? log) =>
        new(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settingsPath) ?? ".", FileName)) { Log = log };

    /// <summary>The record as saved; null without one or when it can't be read (one log line).</summary>
    public FirstRunProgress? Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return null;
            }

            var progress = JsonSerializer.Deserialize<FirstRunProgress>(File.ReadAllText(Path), Json);
            if (progress is null || !Enum.IsDefined(progress.Step) || progress.SwitchedOff is null
                || (progress.Step == FirstRunStep.DataSources && progress.Chosen is null))
            {
                Log?.Invoke("Ersteinrichtung: Stand unvollständig, sie beginnt von vorn"); // not a UI text (log)
                return null;
            }

            return progress;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Log?.Invoke($"Ersteinrichtung: Stand nicht lesbar ({e.GetType().Name}), sie beginnt von vorn"); // not a UI text (log)
            return null;
        }
    }

    /// <summary>Keeps <paramref name="progress"/>; false if it couldn't be written (the first start then goes on without it).</summary>
    public bool Save(FirstRunProgress progress)
    {
        var temp = Path + ".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(progress, Json));
            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log?.Invoke($"Ersteinrichtung: Stand nicht gespeichert ({e.GetType().Name})"); // not a UI text (log)
            return false;
        }
    }

    /// <summary>The first start is over: the record goes, so it can't bring it back.</summary>
    public void Delete()
    {
        try
        {
            File.Delete(Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Harmless: a record is read only while settings.json has no launcher list.
            Log?.Invoke($"Ersteinrichtung: Stand nicht gelöscht ({e.GetType().Name})"); // not a UI text (log)
        }
    }
}
