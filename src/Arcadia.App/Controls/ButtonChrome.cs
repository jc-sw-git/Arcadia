using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// The border a <c>Button.plain</c> draws itself with (#636). A type of its own on purpose: Avalonia matches every
/// <c>Border.x</c> style of the theme (about 200) against every border and keeps a record of each match, and every button
/// has one of these. Styled only by <c>/template/ ctl|ButtonChrome#Chrome</c> selectors.
/// </summary>
public sealed class ButtonChrome : Border;
