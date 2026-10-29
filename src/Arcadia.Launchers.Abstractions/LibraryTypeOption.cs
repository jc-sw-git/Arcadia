namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// A switch on the launcher's page in the settings (#141) that adds entries of <paramref name="Types"/> to its library,
/// off until the user switches it on. Entries of a type no option of the launcher names are always shown; games always.
/// </summary>
/// <param name="Id">Stable within the launcher; the settings keep it.</param>
/// <param name="Title">The row's name, e.g. "Werkzeuge einbeziehen".</param>
/// <param name="Subtitle">One line below the name, e.g. what kind of entries it adds.</param>
/// <param name="Info">The info panel's text about what the switch adds; <paramref name="Examples"/> follow it as a list.</param>
/// <param name="Examples">Entries the switch adds, e.g. "SteamVR".</param>
public sealed record LibraryTypeOption(string Id, string Title, string Subtitle, string Info, IReadOnlyList<string> Examples, IReadOnlyList<GameType> Types);
