using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// A text on an on-screen key (#636). A type of its own on purpose: Avalonia matches every <c>TextBlock.x</c> style of the
/// theme (about 200) against every text block and keeps a record of each match, and the three keyboards built at all
/// times hold several hundred texts. Styled only by <c>ctl|KeyLabel</c> selectors.
/// </summary>
public sealed class KeyLabel : TextBlock;
