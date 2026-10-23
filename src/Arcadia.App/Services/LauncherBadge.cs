using Avalonia.Media;

namespace Arcadia.App.Services;

/// <summary>
/// How a launcher shows wherever Arcadia names it (#138): the picture the user picked, else the launcher's own logo drawn
/// in the text colour, else the first letter of its name on a coloured tile. Drawn by <see cref="Controls.LauncherLogo"/>.
/// </summary>
/// <param name="Shape">The launcher's logo in a 24 × 24 box, or null.</param>
/// <param name="Picture">The user's logo, or null.</param>
public sealed record LauncherBadge(string Monogram, IBrush TileBrush, Geometry? Shape, IImage? Picture);
