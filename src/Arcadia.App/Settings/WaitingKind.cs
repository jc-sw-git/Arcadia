namespace Arcadia.App.Settings;

/// <summary>
/// What a game waits for on "Warten auf deine Auswahl": its SteamGridDB game (artwork, #491) or its IGDB game (metadata,
/// #494). Each tab of "Datenquellen" has its own "Bei mehreren Treffern" and its own waiting list (#538).
/// </summary>
public enum WaitingKind
{
    Artwork,
    Metadata,
}
