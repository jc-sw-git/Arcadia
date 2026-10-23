namespace Arcadia.Core.Settings;

/// <summary>
/// What the user changed about one launcher on its page in the settings (#137, #138); null keeps what the launcher brings.
/// </summary>
/// <param name="Name">"Name im Tab", checked by <see cref="Launchers.LauncherNames.NameProblem"/>.</param>
/// <param name="Keyword">"Such-Kürzel" without the <c>/</c>, checked by <see cref="Launchers.LauncherNames.KeywordProblem"/>.</param>
/// <param name="LogoFile">The file name of the user's own logo in Arcadia's logo folder; null shows the launcher's own.</param>
/// <param name="Folder">The launcher's folder picked by hand, for a launcher Arcadia doesn't find by itself.</param>
public sealed record LauncherPreferences(string? Name = null, string? Keyword = null, string? LogoFile = null, string? Folder = null)
{
    /// <summary>Nothing changed: the entry can go.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Name is null && Keyword is null && LogoFile is null && Folder is null;
}
