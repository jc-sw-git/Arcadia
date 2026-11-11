using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using System.Text;

namespace Arcadia.Launchers.Abstractions.Texts;

/// <summary>
/// The one place every text the user sees is read and filled (#480). Texts live in <c>*Texts.resx</c> files; the
/// classes generated from them (<c>AppTexts</c>, <c>SteamTexts</c>, …) call this. It lives in the launcher contract so a
/// plugin can bring its own resource file and fill it the same way.
/// <para>
/// It never throws: a placeholder without a value stays visible as written (<c>{game}</c>), a missing text shows its
/// key. A text can break like this only after a change slipped past the tests, e.g. in a later translation, so each
/// break is logged once per run, with the key and the placeholder's name but never the values, which can be game
/// names, paths or account names.
/// </para>
/// </summary>
public static class UiText
{
    private static readonly ConcurrentDictionary<string, bool> Reported = new(StringComparer.Ordinal);

    /// <summary>Where a broken text is reported; the App points it at <c>arcadia.log</c>.</summary>
    public static Action<string>? Log { get; set; }

    /// <summary>The text <paramref name="key"/> of <paramref name="resources"/>, or the key when there is none.</summary>
    public static string Get(ResourceManager resources, string key)
    {
        string? text;
        try
        {
            text = resources.GetString(key, CultureInfo.CurrentUICulture);
        }
        catch (Exception ex) when (ex is MissingManifestResourceException or MissingSatelliteAssemblyException or InvalidOperationException)
        {
            text = null;
        }

        if (text is not null)
        {
            return text;
        }

        ReportOnce($"key\n{key}", $"UI-Text fehlt: {key}"); // not a UI text (log)
        return key;
    }

    /// <summary>
    /// The text <paramref name="key"/> with each <c>{name}</c> or <c>{name:format}</c> replaced by the value of that
    /// name. Numbers are formatted with the current culture, as string interpolation does. Values are inserted as they
    /// are, so a value that holds braces is shown literally; a value the text doesn't use is ignored, since a
    /// translation may leave a placeholder out.
    /// </summary>
    public static string Fill(ResourceManager resources, string key, params ReadOnlySpan<(string Name, object? Value)> values)
    {
        var text = Get(resources, key);
        var filled = new StringBuilder(text.Length + 32);
        var position = 0;
        while (position < text.Length)
        {
            var open = text.IndexOf('{', position);
            var close = open < 0 ? -1 : text.IndexOf('}', open + 1);
            if (close < 0)
            {
                filled.Append(text, position, text.Length - position);
                break;
            }

            filled.Append(text, position, open - position);
            var placeholder = text.AsSpan(open + 1, close - open - 1);
            var colon = placeholder.IndexOf(':');
            var name = colon < 0 ? placeholder : placeholder[..colon];
            var format = colon < 0 ? null : placeholder[(colon + 1)..].ToString();
            if (TryFind(values, name, out var value))
            {
                filled.Append(Format(value, format));
            }
            else
            {
                filled.Append(text, open, close - open + 1);
                if (IsName(name))
                {
                    ReportOnce($"value\n{key}\n{name}", $"UI-Text {key}: kein Wert für {{{name}}}"); // not a UI text (log)
                }
            }

            position = close + 1;
        }

        return filled.ToString();
    }

    private static bool TryFind(ReadOnlySpan<(string Name, object? Value)> values, ReadOnlySpan<char> name, out object? value)
    {
        foreach (var (candidate, candidateValue) in values)
        {
            if (name.SequenceEqual(candidate))
            {
                value = candidateValue;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string Format(object? value, string? format) => value switch
    {
        null => string.Empty,
        IFormattable formattable => formattable.ToString(format, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? string.Empty,
    };

    // Only an identifier is a placeholder; anything else in braces is text and is neither filled nor reported.
    private static bool IsName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty || !char.IsAsciiLetter(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static void ReportOnce(string what, string message)
    {
        if (!Reported.TryAdd(what, true))
        {
            return;
        }

        try
        {
            Log?.Invoke(message);
        }
        catch (Exception)
        {
            // Reporting a broken text must not break the page that shows it; the text itself is already shown.
        }
    }
}
