namespace Arcadia.App.Status;

/// <summary>A status message's level in the status modal (#166), ranked for "Dringlichkeit": the highest first.</summary>
public enum StatusLevel
{
    Info,

    /// <summary>A newer Arcadia: between the warnings and the information (owner).</summary>
    Update,
    Warning,
    Error,
}

/// <summary>Where an entry's buttons lead: which category, which row, which fix.</summary>
public enum StatusTarget
{
    /// <summary>A Steam Controller message: its row in "Controller", its fix started there.</summary>
    ControllerIssue,

    /// <summary>"Pan läuft nicht": the controller row that says why; no fix of its own.</summary>
    PanProblem,

    /// <summary>The Arcadia update: "Jetzt aktualisieren" in "Über Arcadia", or straight from the modal.</summary>
    ArcadiaUpdate,

    /// <summary>SteamGridDB turned the key down (#489): "Schlüssel" in "Datenquellen"; no fix of its own.</summary>
    SteamGridDbKey,
}

/// <summary>
/// One message the header shows, as an entry of the status modal (#166).
/// </summary>
/// <param name="Id">Stays the same while the message is shown; the focus follows it when the list changes.</param>
/// <param name="Since">When it appeared or last changed its level.</param>
/// <param name="FixLabel">Its fix's button, e.g. "Installieren"; null: only "Zu den Einstellungen".</param>
public sealed record StatusEntry(string Id, StatusLevel Level, string Title, string ShortText, DateTimeOffset Since, StatusTarget Target, string? FixLabel);
