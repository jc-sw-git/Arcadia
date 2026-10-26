using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arcadia.Pan;

/// <summary>A process Pan holds a handle to, so that ending it later can never hit a process that reused the id.</summary>
public sealed record HeldProcess(int Pid, long StartTime, string ImageName);

/// <summary>
/// What Pan would have to undo right now, written before each change (#243, review point 8): the client it serves,
/// the processes it holds, and exactly the HidHide entries it added (part B). Entries the user had before
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

    /// <summary>The HidHide device instances Pan added (part B); devices the user had hidden before are never listed.</summary>
    public List<string> HiddenDevices { get; set; } = [];

    /// <summary>The allow-list entries Pan added: the client's image path, unless the user had listed it already.</summary>
    public List<string> AllowListed { get; set; } = [];

    /// <summary>Whether cloaking was on before Pan hid; null while Pan hides nothing. Cloaking is turned off again only if it was off.</summary>
    public bool? CloakingWasOn { get; set; }

    /// <summary>The Steam Controller still has to be restarted, so Windows announces it again (after a hide, unhide or arrival).</summary>
    public bool PuckRestartPending { get; set; }

    /// <summary>Pan hides, or has a HidHide change or a restart left to undo or finish.</summary>
    [JsonIgnore]
    public bool HidingHasWork => HiddenDevices.Count > 0 || AllowListed.Count > 0 || CloakingWasOn is not null || PuckRestartPending;

    [JsonIgnore]
    public bool HasWork => Emulating || HeldProcesses.Count > 0 || HidingHasWork;

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
        PuckRestartPending = PuckRestartPending,
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

    /// <summary>
    /// Reads the journal. A fallback (the previous version, or nothing) is written back as the journal at once, so the
    /// next load sees the same state and the generation it reached: nothing is undone twice or counted from 1 again.
    /// </summary>
    public JournalLoad Load()
    {
        var name = System.IO.Path.GetFileName(Path);
        if (!File.Exists(Path))
        {
            // Only a failed write-back (below) or a hand leaves the previous version without a journal: it still counts.
            if (File.Exists(PreviousPath) && TryRead(PreviousPath, out var kept, out _))
            {
                return new(kept, $"{name} missing; using the previous version{Promote(kept)}", FromPrevious: true);
            }

            return new(new JournalState(), null, FromPrevious: false);
        }

        if (TryRead(Path, out var state, out var problem))
        {
            return new(state, null, FromPrevious: false);
        }

        MoveAside(Path, BrokenPath);
        if (File.Exists(PreviousPath) && TryRead(PreviousPath, out var previous, out _))
        {
            return new(previous, $"{name} unreadable ({problem}); using the previous version{Promote(previous)}", FromPrevious: true);
        }

        var empty = new JournalState();
        return new(empty, $"{name} unreadable ({problem}) and no previous version; nothing to undo is known{Promote(empty)}", FromPrevious: false);
    }

    /// <summary>Makes the fallback the journal; a failure is reported, never thrown.</summary>
    private string Promote(JournalState fallback)
    {
        try
        {
            Save(fallback);
            return string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"; it could not be written back ({ex.Message})";
        }
    }

    private static bool TryRead(string file, out JournalState state, out string? problem)
    {
        try
        {
            using var stream = File.OpenRead(file);
            state = JsonSerializer.Deserialize(stream, JournalJson.Default.JournalState) ?? throw new JsonException("empty");
            if (Validate(state) is { } invalid)
            {
                throw new JsonException(invalid);
            }

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

    /// <summary>
    /// What deserializing alone lets through: a null where a list belongs, an entry without a process, an unknown
    /// version. Such a journal counts as unreadable (review point 4), so the previous version is used instead of Pan
    /// failing at every start on the same file.
    /// </summary>
    private static string? Validate(JournalState state)
    {
        if (state.Version != 1)
        {
            return $"unknown journal version {state.Version}";
        }

        if (state.HeldProcesses is null || state.HiddenDevices is null || state.AllowListed is null)
        {
            return "a list is missing";
        }

        if (state.HeldProcesses.Any(process => process is null || process.Pid <= 0 || process.ImageName is null))
        {
            return "a held process entry is incomplete";
        }

        if (state.HiddenDevices.Any(string.IsNullOrEmpty) || state.AllowListed.Any(string.IsNullOrEmpty))
        {
            return "a HidHide entry is missing";
        }

        // Pan only ever hides listed devices; anything else would make an undo remove one of the user's entries.
        if (state.HiddenDevices.Any(device => !HidingList.IsListedHid(device)))
        {
            return "a hidden device is not one Pan hides";
        }

        return null;
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
