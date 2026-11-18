using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadia.Core.Localization;
using Arcadia.Core.Search;

namespace Arcadia.Core.Settings;

/// <summary>How Arcadia shows a time of day (#553): stored as its number, so keep the values.</summary>
public enum ClockFormat
{
    /// <summary>"Wie im System": the system's time format.</summary>
    System = 0,

    /// <summary>"24 Stunden": 21:58.</summary>
    TwentyFourHours = 1,

    /// <summary>"12 Stunden": 9:58 PM.</summary>
    TwelveHours = 2,
}

/// <summary>
/// The category "Lokalisierung" (#14, #553, #554): the UI language, the on-screen keyboard's layouts and the clock. Each value is read leniently
/// (<see cref="Lenient"/>): one of the wrong type, written by hand, must not cost the player the rest of settings.json.
/// </summary>
public sealed record LocalizationSettings
{
    /// <summary>
    /// "Sprache": <see cref="UiLanguages.System"/> or a language's code (<see cref="UiLanguages.Choices"/>); null until the
    /// player chooses one, which is the system language too. Read at the start only: a change applies after a restart.
    /// </summary>
    [JsonConverter(typeof(Lenient.Choice))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // a null written out would read back as unreadable
    public string? Language { get; init; }

    /// <summary>"Standard-Layout": the id of the layout every on-screen keyboard opens with; null until the player chooses one.</summary>
    [JsonConverter(typeof(Lenient.Text))]
    public string? KeyboardLayout { get; init; }

    /// <summary>
    /// The layouts switched on for R3, by id; null until the player switches one: then all five are in it. The default
    /// layout is always in the quick selection, listed here or not (<see cref="KeyboardLayoutChoice"/>).
    /// </summary>
    [JsonConverter(typeof(Lenient.TextList))]
    public IReadOnlyList<string>? QuickLayouts { get; init; }

    /// <summary>"Uhrzeit".</summary>
    [JsonConverter(typeof(Lenient.Clock))]
    public ClockFormat Clock { get; init; }

    /// <summary>
    /// Replaces what this version doesn't know (a newer Arcadia wrote it, or it was edited by hand) with the default,
    /// one log line per value, so a broken value never fails later.
    /// </summary>
    public LocalizationSettings WithValidValues(Action<string> log)
    {
        var language = Language;
        if (language is not null && !UiLanguages.IsChoice(language))
        {
            log($"Lokalisierung: unbekannte Sprache „{Shorten(language)}“, Arcadia nimmt die Systemsprache"); // not a UI text (log)
            language = null;
        }

        var layout = KeyboardLayout;
        if (layout is not null && Search.KeyboardLayout.TryFind(layout) is null)
        {
            log($"Lokalisierung: unbekanntes Standard-Layout „{Shorten(layout)}“, Arcadia nimmt das des Systems"); // not a UI text (log)
            layout = null;
        }

        var quick = QuickLayouts;
        if (quick is not null && (quick.Any(id => Search.KeyboardLayout.TryFind(id) is null) || quick.Distinct().Count() != quick.Count))
        {
            log("Lokalisierung: unbekannte oder doppelte Layouts in der Schnellauswahl weggelassen"); // not a UI text (log)
            quick = [.. quick.Where(id => Search.KeyboardLayout.TryFind(id) is not null).Distinct()];

            // Nothing known left (a list of the wrong type, or only unknown ids) is no choice of the player: all five.
            if (quick.Count == 0)
            {
                quick = null;
            }
        }

        var clock = Clock;
        if (!Enum.IsDefined(clock))
        {
            log("Lokalisierung: unbekannte Uhrzeit, Arcadia nimmt „Wie im System“"); // not a UI text (log)
            clock = ClockFormat.System;
        }

        return this with { Language = language, KeyboardLayout = layout, QuickLayouts = quick, Clock = clock };
    }

    /// <summary>
    /// "Standard-Layout" on <paramref name="layout"/>; the default before it stays in the quick selection (its switch on,
    /// now changeable), as does every layout that was in it.
    /// </summary>
    /// <param name="before">The layout the row stood on, also when it came from the system and was never saved.</param>
    public LocalizationSettings WithDefaultLayout(KeyboardLayout layout, KeyboardLayout before)
    {
        var quick = QuickLayouts is null ? null : (IReadOnlyList<string>)[.. QuickLayouts.Append(before.Id).Distinct()];
        return this with { KeyboardLayout = layout.Id, QuickLayouts = quick };
    }

    /// <summary>Switches <paramref name="layout"/> in or out of the quick selection; the first switch writes the whole list.</summary>
    public LocalizationSettings WithQuickLayout(KeyboardLayout layout, bool on)
    {
        var quick = QuickLayouts ?? [.. Search.KeyboardLayout.All.Select(l => l.Id)];
        return this with
        {
            QuickLayouts = on ? [.. quick.Append(layout.Id).Distinct()] : [.. quick.Where(id => id != layout.Id)],
        };
    }

    // A value edited by hand may be anything; the log gets only its start.
    private static string Shorten(string value) => value.Length <= 16 ? value : value[..16] + "…";

    /// <summary>
    /// Readers that take a value of the wrong JSON type as a value this version doesn't know, which
    /// <see cref="WithValidValues"/> then replaces and logs, instead of failing the whole file.
    /// </summary>
    public static class Lenient
    {
        // No layout has an empty id, so "" stands for "something unreadable was there".
        private const string Unreadable = "";

        public sealed class Text : JsonConverter<string?>
        {
            public override bool HandleNull => true;

            public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    return reader.GetString();
                }

                reader.Skip();
                return Unreadable;
            }

            public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(writer, value, options);
        }

        /// <summary>Like <see cref="Text"/>, but a null written out is unreadable too: only a missing value is "not chosen".</summary>
        public sealed class Choice : JsonConverter<string?>
        {
            public override bool HandleNull => true;

            public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    return reader.GetString();
                }

                reader.Skip();
                return Unreadable;
            }

            public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(writer, value, options);
        }

        public sealed class TextList : JsonConverter<IReadOnlyList<string>?>
        {
            public override bool HandleNull => true;

            public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null;
                }

                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    reader.Skip();
                    return [Unreadable];
                }

                var items = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    items.Add(reader.TokenType == JsonTokenType.String ? reader.GetString()! : Unreadable);
                    reader.Skip();
                }

                return items;
            }

            public override void Write(Utf8JsonWriter writer, IReadOnlyList<string>? value, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(writer, value, options);
        }

        public sealed class Clock : JsonConverter<ClockFormat>
        {
            public override ClockFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value))
                {
                    return (ClockFormat)value;
                }

                reader.Skip();
                return (ClockFormat)(-1);
            }

            public override void Write(Utf8JsonWriter writer, ClockFormat value, JsonSerializerOptions options) => writer.WriteNumberValue((int)value);
        }
    }
}
