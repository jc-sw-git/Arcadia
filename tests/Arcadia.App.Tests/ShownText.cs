using System.Collections;
using Arcadia.App.Settings;

namespace Arcadia.App.Tests;

/// <summary>Every text a settings row shows: title, subtitle, value and its info panel, boxes inside boxes included.</summary>
internal static class ShownText
{
    public static IEnumerable<string> Of(SettingsRowViewModel row)
    {
        yield return row.Title;
        if (row.Subtitle is { } subtitle)
        {
            yield return subtitle;
        }

        if (row is ChoiceRowViewModel choice)
        {
            yield return choice.ValueText;
        }

        foreach (var text in row.Info.Concat(row.Preview).SelectMany(Of))
        {
            yield return text;
        }
    }

    /// <summary>The strings of an info block's properties, and of the blocks and lists it holds.</summary>
    public static IEnumerable<string> Of(InfoBlock block)
    {
        foreach (var property in block.GetType().GetProperties())
        {
            switch (property.GetValue(block))
            {
                case string text:
                    yield return text;
                    break;
                case InfoBlock inner:
                    foreach (var text in Of(inner))
                    {
                        yield return text;
                    }

                    break;
                case IEnumerable items:
                    foreach (var item in items)
                    {
                        if (item is string text)
                        {
                            yield return text;
                        }
                        else if (item is InfoBlock innerBlock)
                        {
                            foreach (var innerText in Of(innerBlock))
                            {
                                yield return innerText;
                            }
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>The blocks of an info panel and every block inside them, e.g. to find a box or a card.</summary>
    public static IEnumerable<InfoBlock> Blocks(IEnumerable<InfoBlock> blocks) =>
        blocks.SelectMany(block => block is InfoSection section ? [block, .. Blocks(section.Blocks)] : new[] { block });
}
