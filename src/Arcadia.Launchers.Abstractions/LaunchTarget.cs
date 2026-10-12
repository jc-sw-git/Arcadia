namespace Arcadia.Launchers.Abstractions;

/// <summary>Where and how a game is started.</summary>
public abstract record LaunchTarget
{
    private LaunchTarget()
    {
    }

    /// <summary>A concrete executable with arguments and an optional working directory.</summary>
    public sealed record Executable(string Path, IReadOnlyList<string> Arguments, string? WorkingDirectory) : LaunchTarget;

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
