namespace Arcadia.Launchers.Abstractions;

/// <summary>A start the launcher itself aborted (#277), e.g. because the signed-in account owns no license for the game.</summary>
/// <param name="GameId">The <see cref="Game.Id"/>.</param>
/// <param name="At">When the launcher aborted it; an abort from before Arcadia asked for the start doesn't count.</param>
/// <param name="Reason">
/// Why, as a sentence for the player (German for now), e.g. "Das angemeldete Steam-Konto besitzt dieses Spiel nicht.";
/// null if the launcher doesn't say, and Arcadia names only the launcher.
/// </param>
/// <param name="Detail">The launcher's own words for it (an error code), for Arcadia's log only.</param>
public sealed record RefusedStart(string GameId, DateTimeOffset At, string? Reason, string Detail);
