namespace Arcadia.Core.Tests;

/// <summary>Source files of the repository, for tests that check rules on the code itself rather than its behaviour.</summary>
internal static class RepositoryFiles
{
    public static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Arcadia.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("Arcadia.slnx not found above the test's folder.");
    }

    /// <summary>The files of a project folder below <c>src/</c>, without build output.</summary>
    public static IEnumerable<string> SourceFiles(string project, string pattern)
    {
        var folder = Path.Combine(Root(), "src", project);
        var buildOutput = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        return Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains(buildOutput, StringComparison.Ordinal));
    }
}
