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
/// It never throws: a placeholder without a value stays visible as written (<c>{game}</c>), a format the value doesn't
/// know is left out, a text missing in a translation shows in English, a text missing altogether shows its key. A text can break like this only after a change slipped past the tests, e.g. in a later translation, so each
/// break is logged once per run, with the key and the placeholder's name but never the values, which can be game
/// names, paths or account names.
/// </para>
/// </summary>
public static class UiText
{
    private static readonly ConcurrentDictionary<string, bool> Reported = new(StringComparer.Ordinal);

    /// <summary>The language a text missing in a translation falls back to.</summary>
    public static CultureInfo English { get; } = CultureInfo.GetCultureInfo("en");

    /// <summary>Where a broken text is reported; the App points it at <c>arcadia.log</c>.</summary>
    public static Action<string>? Log { get; set; }

    /// <summary>The text <paramref name="key"/> of <paramref name="resources"/>, or the key when there is none.</summary>
    public static string Get(ResourceManager resources, string key)
    {
        var culture = CultureInfo.CurrentUICulture;
        var text = TextsOf(resources, culture)?.GetString(key);
        if (text is null && culture.TwoLetterISOLanguageName != English.TwoLetterISOLanguageName)
        {
            // A text a translation lacks shows in English, the language every player is most likely to read.
            text = TextsOf(resources, English)?.GetString(key);
            if (text is not null && culture.TwoLetterISOLanguageName != "de")
            {
                ReportOnce($"translation\n{culture.TwoLetterISOLanguageName}\n{key}", $"UI-Text fehlt auf {culture.TwoLetterISOLanguageName}, Arcadia zeigt ihn auf Englisch: {key}"); // not a UI text (log)
            }
        }

        // Last the plain lookup, which also serves a ResourceManager that brings its texts its own way (a test's).
        text ??= GetString(resources, key, culture);
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
                filled.Append(Format(value, format, key, name));
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

    // The texts of the UI language: its translation (fr-FR → fr), or the neutral German texts for German or a language
    // without one. ResourceManager caches what it finds per culture, so this costs a lookup after the first call.
    private static ResourceSet? TextsOf(ResourceManager resources, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(resources.BaseName))
        {
            return null;
        }

        try
        {
            return resources.GetResourceSet(culture, createIfNotExists: true, tryParents: true);
        }
        catch (Exception ex) when (ex is MissingManifestResourceException or MissingSatelliteAssemblyException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? GetString(ResourceManager resources, string key, CultureInfo culture)
    {
        try
        {
            return resources.GetString(key, culture);
        }
        catch (Exception ex) when (ex is MissingManifestResourceException or MissingSatelliteAssemblyException or InvalidOperationException)
        {
            return null;
        }
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

    // A format the value's type doesn't know (a later translation writing {minutes:Q}) must not break the text: the
    // value is shown without it.
    private static string Format(object? value, string? format, string key, ReadOnlySpan<char> name)
    {
        if (value is not IFormattable formattable)
        {
            return value?.ToString() ?? string.Empty;
        }

        try
        {
            return formattable.ToString(format, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            ReportOnce($"format\n{key}\n{name}", $"UI-Text {key}: ungültiges Format für {{{name}}}"); // not a UI text (log)
            return formattable.ToString(null, CultureInfo.CurrentCulture);
        }
    }

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
