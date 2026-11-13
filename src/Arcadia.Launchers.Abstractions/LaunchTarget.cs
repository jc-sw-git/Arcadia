namespace Arcadia.Launchers.Abstractions;

/// <summary>Where and how a game is started.</summary>
public abstract record LaunchTarget
{
    private LaunchTarget()
    {
    }

    /// <summary>
    /// A concrete executable with arguments and an optional working directory. Equal when the arguments are (a record
    /// would compare the list by reference), so a rescan that finds the same game finds an equal one.
    /// </summary>
    public sealed record Executable(string Path, IReadOnlyList<string> Arguments, string? WorkingDirectory) : LaunchTarget
    {
        public bool Equals(Executable? other) =>
            other is not null && Path == other.Path && WorkingDirectory == other.WorkingDirectory && Arguments.SequenceEqual(other.Arguments);

        public override int GetHashCode() => HashCode.Combine(Path, WorkingDirectory, Arguments.Count);
    }

    /// <summary>
    /// A launcher protocol URI, e.g.
    /// <c>com.epicgames.launcher://apps/&lt;ns&gt;%3A&lt;id&gt;%3A&lt;app&gt;?action=launch&amp;silent=true</c>.
    /// </summary>
    public sealed record ProtocolUri(string Uri) : LaunchTarget;

    /// <summary>A packaged (UWP / Game Pass) app, started via <c>shell:appsFolder\&lt;AUMID&gt;</c>.</summary>
    public sealed record ShellAppsFolder(string AppUserModelId) : LaunchTarget;

    /// <summary>Nothing to start — used for demo data.</summary>
    public sealed record None : LaunchTarget;
}
