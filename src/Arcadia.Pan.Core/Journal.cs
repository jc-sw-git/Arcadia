using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arcadia.Pan;

/// <summary>A process Pan holds a handle to, so that ending it later can never hit a process that reused the id.</summary>
public sealed record HeldProcess(int Pid, long StartTime, string ImageName);

/// <summary>
/// What Pan would have to undo right now, written before each change (#243, review point 8): the client it serves,
/// the processes it holds, and (from part B on) exactly the HidHide entries it added. Entries the user had before
/// are never in here, so they are never touched.
/// </summary>
public sealed class JournalState
{
    public int Version { get; set; } = 1;

    /// <summary>The last generation handed out; the next emulation gets the one after it, also after a restart.</summary>
    public uint Generation { get; set; }

    public int ClientPid { get; set; }

    /// <summary>The client's process start time (Windows FILETIME ticks), to tell a live Arcadia from a reused id.</summary>
    public long ClientStartTime { get; set; }

    /// <summary>An emulation is open: Arcadia hasn't said it stopped, and Pan hasn't torn it down completely.</summary>
    public bool Emulating { get; set; }

    public List<HeldProcess> HeldProcesses { get; set; } = [];

    /// <summary>Part B: the HidHide device instances Pan hid, the allow-list entries it added, and whether cloaking was on before.</summary>
    public List<string> HiddenDevices { get; set; } = [];

    public List<string> AllowListed { get; set; } = [];

    public bool? CloakingWasOn { get; set; }

    [JsonIgnore]
    public bool HasWork => Emulating || HeldProcesses.Count > 0 || HiddenDevices.Count > 0 || AllowListed.Count > 0 || CloakingWasOn is not null;

    public JournalState Clone() => new()
    {
        Version = Version,
        Generation = Generation,
        ClientPid = ClientPid,
        ClientStartTime = ClientStartTime,
        Emulating = Emulating,
        HeldProcesses = [.. HeldProcesses],
        HiddenDevices = [.. HiddenDevices],
        AllowListed = [.. AllowListed],
        CloakingWasOn = CloakingWasOn,
    };
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(JournalState))]
internal sealed partial class JournalJson : JsonSerializerContext;

/// <summary>What a load gave: the state, and what was wrong with the file if anything (reported to Arcadia).</summary>
public sealed record JournalLoad(JournalState State, string? Problem, bool FromPrevious);

/// <summary>
/// The journal on disk. A save goes to a temp file with write-through and flush, then replaces the journal and keeps
/// the previous one, so power loss mid-write leaves either the old or the new journal. A journal that can't be read
/// never throws out of here: the previous one is used, the broken one moved aside, and the problem reported.
/// </summary>
public sealed class JournalFile(string path)
{
    public string Path { get; } = path;

    public string PreviousPath => Path + ".previous";

    public string BrokenPath => Path + ".broken";

    private string TempPath => Path + ".tmp";

    public void Save(JournalState state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, state, JournalJson.Default.JournalState);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(Path))
        {
            File.Replace(TempPath, Path, PreviousPath);
        }
        else
        {
            File.Move(TempPath, Path);
        }
    }

    public JournalLoad Load()
    {
        if (!File.Exists(Path))
        {
            return new(new JournalState(), null, FromPrevious: false);
        }

        if (TryRead(Path, out var state, out var problem))
        {
            return new(state, null, FromPrevious: false);
        }

        MoveAside(Path, BrokenPath);
        if (File.Exists(PreviousPath) && TryRead(PreviousPath, out var previous, out _))
        {
            return new(previous, $"{System.IO.Path.GetFileName(Path)} unreadable ({problem}); using the previous version", FromPrevious: true);
        }

        return new(new JournalState(), $"{System.IO.Path.GetFileName(Path)} unreadable ({problem}) and no previous version; nothing to undo is known", FromPrevious: false);
    }

    /// <summary>Nothing left to undo: the journal goes, its previous version stays for a look.</summary>
    public void Clear()
    {
        if (File.Exists(Path))
        {
            File.Move(Path, PreviousPath, overwrite: true);
        }
    }

    private static bool TryRead(string file, out JournalState state, out string? problem)
    {
        try
        {
            using var stream = File.OpenRead(file);
            state = JsonSerializer.Deserialize(stream, JournalJson.Default.JournalState) ?? throw new JsonException("empty");
            problem = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            state = new JournalState();
            problem = ex.Message;
            return false;
        }
    }

    private static void MoveAside(string file, string aside)
    {
        try
        {
            File.Move(file, aside, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left in place; the next save replaces it anyway.
        }
    }
}
