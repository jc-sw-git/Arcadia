namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// A sign-in to the launcher's online account (#83), so that its library also holds the games the user owns but hasn't
/// installed. Shown on the launcher's page as a group "Konto" while the launcher is active and found. The sign-in is a QR
/// code the user approves in the launcher's phone app; Arcadia never sees a password.
/// </summary>
/// <remarks>
/// Events may come from any thread. A change of the owned games raises <see cref="LibraryChanged"/>; the launcher's
/// scanner delivers them, so Arcadia then reads the launcher again.
/// </remarks>
public interface ILauncherAccount
{
    /// <summary>The row's name, e.g. "Steam-Konto".</summary>
    string Title { get; }

    /// <summary>The row's line below its name while signed out: what the sign-in is good for.</summary>
    string Purpose { get; }

    /// <summary>The info panel's text while signed out.</summary>
    string About { get; }

    /// <summary>What the sign-in needs, e.g. "Benötigt die Steam-App auf dem Handy".</summary>
    string Requirement { get; }

    /// <summary>The info panel's text beside the QR code: where in the phone app to scan it.</summary>
    string CodeInstruction { get; }

    LauncherAccountState State { get; }

    /// <summary><see cref="State"/> changed.</summary>
    event EventHandler? StateChanged;

    /// <summary>The owned games changed (fetched, signed in or out): the launcher's library should be read again.</summary>
    event EventHandler? LibraryChanged;

    /// <summary>Background work failed unexpectedly (a bug, not Steam being unreachable); Arcadia logs and shows it.</summary>
    event EventHandler<Exception>? Failed;

    /// <summary>Starts a sign-in: a QR code to approve, renewed by itself until it is approved, rejected or expires.</summary>
    void BeginSignIn();

    /// <summary>Drops a sign-in that is under way; the account stays as it was.</summary>
    void CancelSignIn();

    /// <summary>Forgets the sign-in and the owned games.</summary>
    void SignOut();

    /// <summary>The user asked to read the launcher again ("Neu einlesen"): fetch the owned games now, not when due.</summary>
    void RefreshNow();
}

/// <summary>Where a sign-in stands.</summary>
public enum AccountStep
{
    SignedOut,

    /// <summary>The QR code is being fetched.</summary>
    Starting,

    /// <summary><see cref="LauncherAccountState.Code"/> waits for approval on the phone.</summary>
    ShowingCode,

    /// <summary>Nobody approved the code in time.</summary>
    CodeExpired,

    /// <summary>The code was rejected on the phone.</summary>
    CodeRejected,

    /// <summary>The launcher's sign-in service couldn't be reached.</summary>
    Unreachable,

    SignedIn,
}

/// <summary>The account as the launcher's page shows it.</summary>
public sealed record LauncherAccountState(AccountStep Step)
{
    public static readonly LauncherAccountState SignedOut = new(AccountStep.SignedOut);

    /// <summary>The name the account goes by, e.g. the Steam profile name; null while signed out.</summary>
    public string? AccountName { get; init; }

    /// <summary>The text of the QR code while <see cref="AccountStep.ShowingCode"/>.</summary>
    public string? Code { get; init; }

    /// <summary>
    /// A status message (warning) about the account, e.g. the owned games couldn't be fetched or the sign-in expired;
    /// null if all is well.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>Information about the account, e.g. that the launcher is signed in with another account; null if none.</summary>
    public string? Notice { get; init; }

    /// <summary>How many games the account owns, as fetched last; null if never fetched.</summary>
    public int? OwnedGames { get; init; }

    /// <summary>When the owned games were fetched last.</summary>
    public DateTimeOffset? ListedAt { get; init; }

    /// <summary>A sign-in is under way or ended without success: B drops it.</summary>
    public bool IsSigningIn => Step is AccountStep.Starting or AccountStep.ShowingCode or AccountStep.CodeExpired or AccountStep.CodeRejected or AccountStep.Unreachable;
}
