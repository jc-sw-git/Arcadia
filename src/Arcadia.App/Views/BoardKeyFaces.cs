using Arcadia.App.Keyboard;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Arcadia.App.Views;

/// <summary>
/// The face of an on-screen key by its kind (#636): a key builds only the parts of its own kind, not those of the other
/// kinds hidden. Three keyboards stay built at all times (#477), so every element per key counts in memory. A key's kind
/// never changes: a new layout brings new keys.
/// </summary>
public sealed class BoardKeyFaces : IDataTemplate
{
    /// <summary>A character key: its label with the shift and AltGr characters beside it.</summary>
    [Content]
    public IDataTemplate? Character { get; set; }

    /// <summary>Backspace, tab, shift, space, AltGr, the right block and the arrows: symbol or icon, name and button.</summary>
    public IDataTemplate? Labelled { get; set; }

    /// <summary>The tall ISO Enter over two rows.</summary>
    public IDataTemplate? TallEnter { get; set; }

    public bool Match(object? data) => data is BoardKeyViewModel;

    /// <summary>Caps lock is blank: it gets no face.</summary>
    public Control? Build(object? param) => param is BoardKeyViewModel key ? FaceOf(key)?.Build(key) : null;

    private IDataTemplate? FaceOf(BoardKeyViewModel key) =>
        key.IsCharacter ? Character
        : key.IsTallEnter ? TallEnter
        : key.IsLabelled ? Labelled
        : null;
}
