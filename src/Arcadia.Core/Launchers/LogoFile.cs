using System.Text;
using System.Xml;

namespace Arcadia.Core.Launchers;

/// <summary>The kinds of picture a launcher logo may be (#138).</summary>
public enum LogoFormat
{
    Png,
    Svg,
    Ico,
    WebP,
}

/// <summary>A picture the user picked as a launcher's logo, checked: its kind, or why it can't be one.</summary>
public sealed record LogoCheck(LogoFormat? Format, string? Problem)
{
    public bool IsValid => Format is not null;
}

/// <summary>
/// Checks a file the user picked as a launcher's logo (#138) before Arcadia reads it as a picture: its size, its kind by
/// its content (not its name), and for SVG that it is a picture alone: no scripts, no other files or pages it would load,
/// no document type that could pull such in. The file is copied into Arcadia's own folder only after this.
/// </summary>
public static class LogoFile
{
    /// <summary>A logo is a small picture; anything bigger is likely not one.</summary>
    public const long MaxBytes = 2 * 1024 * 1024;

    private static ReadOnlySpan<byte> PngSignature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>What the file dialog offers and the info panel names.</summary>
    public static IReadOnlyList<string> Extensions { get; } = ["png", "svg", "ico", "webp"];

    /// <param name="length">The file's size in bytes, checked before it is read.</param>
    public static string? SizeProblem(long length) =>
        length == 0 ? "Die Datei ist leer."
        : length > MaxBytes ? $"Die Datei ist größer als {MaxBytes / (1024 * 1024)} MB."
        : null;

    public static LogoCheck Check(ReadOnlySpan<byte> content)
    {
        if (SizeProblem(content.Length) is { } sizeProblem)
        {
            return new LogoCheck(null, sizeProblem);
        }

        if (content.StartsWith(PngSignature))
        {
            return new LogoCheck(LogoFormat.Png, null);
        }

        if (content.Length >= 12 && content.StartsWith("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
        {
            return new LogoCheck(LogoFormat.WebP, null);
        }

        // An icon file: reserved 0, type 1, then at least one picture.
        if (content.Length >= 6 && content[0] == 0 && content[1] == 0 && content[2] == 1 && content[3] == 0 && (content[4] | content[5]) != 0)
        {
            return new LogoCheck(LogoFormat.Ico, null);
        }

        return SvgProblem(content) switch
        {
            null => new LogoCheck(LogoFormat.Svg, null),
            var problem => new LogoCheck(null, problem),
        };
    }

    /// <returns>Null for an SVG that is a picture alone; otherwise why not, or that the file is no picture Arcadia takes.</returns>
    private static string? SvgProblem(ReadOnlySpan<byte> content)
    {
        const string NotAPicture = "Das ist keine PNG-, SVG-, ICO- oder WebP-Datei.";
        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return NotAPicture;
        }

        var settings = new XmlReaderSettings
        {
            // A document type could load other files or swell to gigabytes (entity expansion).
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
        };
        try
        {
            using var reader = XmlReader.Create(new StringReader(text.TrimStart('﻿')), settings);
            var isFirst = true;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.ProcessingInstruction && reader.Name != "xml")
                {
                    return "Das SVG lädt ein Stylesheet; Arcadia nimmt nur reine Bilder.";
                }

                if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA)
                {
                    if (LoadsSomething(reader.Value))
                    {
                        return "Das SVG lädt andere Dateien oder Seiten; Arcadia nimmt nur Bilder ohne Verweise nach außen.";
                    }

                    continue;
                }

                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (isFirst && reader.LocalName != "svg")
                {
                    return NotAPicture;
                }

                isFirst = false;
                if (reader.LocalName is "script" or "foreignObject" or "iframe" or "object" or "embed")
                {
                    return "Das SVG enthält Skripte oder Webinhalte; Arcadia nimmt nur reine Bilder.";
                }

                if (reader.LocalName is "image" or "feImage")
                {
                    return "Das SVG bettet andere Bilder ein; Arcadia nimmt nur reine Vektorgrafik.";
                }

                while (reader.MoveToNextAttribute())
                {
                    var external = reader.LocalName == "href" ? !reader.Value.TrimStart().StartsWith('#') : LoadsSomething(reader.Value);
                    if (external || reader.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Das SVG lädt andere Dateien oder Seiten; Arcadia nimmt nur Bilder ohne Verweise nach außen.";
                    }
                }

                reader.MoveToElement();
            }

            return isFirst ? NotAPicture : null;
        }
        catch (XmlException)
        {
            return NotAPicture;
        }
    }

    /// <summary>A style or attribute that points outside the picture: <c>url(…)</c> other than to its own parts, or <c>@import</c>.</summary>
    private static bool LoadsSomething(string value)
    {
        if (value.Contains("@import", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var at = value.IndexOf("url(", StringComparison.OrdinalIgnoreCase); at >= 0; at = value.IndexOf("url(", at + 4, StringComparison.OrdinalIgnoreCase))
        {
            var target = value[(at + 4)..].TrimStart().TrimStart('\'', '"');
            if (!target.StartsWith('#'))
            {
                return true;
            }
        }

        return false;
    }
}
