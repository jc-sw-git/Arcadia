namespace Arcadia.Core.Diagnostics;

/// <summary>
/// Keeps the user's name out of <c>arcadia.log</c>, which users attach to bug reports: paths in messages and
/// stack traces (<c>C:\Users\Name\…</c>) name the Windows account. The log still shows where a file lies.
/// </summary>
public static class LogPrivacy
{
    public const string ProfilePlaceholder = "%USERPROFILE%";

    /// <param name="userProfile">The user's profile folder, e.g. <c>C:\Users\Name</c>; nothing is replaced without one.</param>
    public static string WithoutUserProfile(string message, string? userProfile)
    {
        var profile = userProfile?.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(profile) || profile.Length < 4)
        {
            // A root or nothing: replacing it would blank out unrelated paths.
            return message;
        }

        // Some programs (Steam, Epic) write the same path with forward slashes.
        return message
            .Replace(profile, ProfilePlaceholder, StringComparison.OrdinalIgnoreCase)
            .Replace(profile.Replace('\\', '/'), ProfilePlaceholder, StringComparison.OrdinalIgnoreCase);
    }
}
