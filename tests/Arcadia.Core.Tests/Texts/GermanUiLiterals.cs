using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Arcadia.Core.Tests.Texts;

/// <summary>
/// Finds German texts written into code or views instead of a <c>*Texts.resx</c> (#480). A string counts as German by
/// its umlauts and quotes or by a common German word, so "Abbrechen" counts too. Log lines and exception messages stay
/// in the code: they are diagnostics, not texts the player reads.
/// </summary>
internal static partial class GermanUiLiterals
{
    /// <summary>The comment that marks a line's German string as not shown, e.g. a reason only logged.</summary>
    public const string Marker = "// not a UI text";

    // View attributes that show text; any letters there must come from a resource, apart from names and keys.
    private static readonly HashSet<string> TextAttributes = ["Text", "Content", "ToolTip.Tip", "Watermark", "Title", "Header", "PlaceholderText"];

    private static readonly HashSet<string> NotTranslated = ["Arcadia", "ARCADIA", "Esc", "Enter", "OK", "i", "⇧ / AltGr"];

    // Calls whose strings are written to a log; Write counts only on AppLog.
    private static readonly HashSet<string> LogCalls = ["Log", "log", "_log", "LogOnce", "_warn", "warn", "TraceWarning", "TraceInformation", "TraceError", "WriteLine", "Fail", "Assert"];

    public static bool LooksGerman(string text) => GermanMarks().IsMatch(text) || GermanWord().IsMatch(text);

    /// <summary>
    /// "line: text" for each German string of the C# source that is neither logged nor an exception message. A string
    /// that reaches only the log on another way (a reason passed on, a description for <c>arcadia.log</c>) is marked
    /// with <see cref="Marker"/> on its line.
    /// </summary>
    public static IEnumerable<string> InCSharp(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var lines = source.Split('\n');
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            var text = node switch
            {
                LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
                InterpolatedStringTextSyntax part => part.TextToken.ValueText,
                _ => null,
            };
            var line = tree.GetLineSpan(node.Span).StartLinePosition.Line;
            if (text is not null && LooksGerman(text) && !IsDiagnostic(node) && !lines[line].Contains(Marker, StringComparison.Ordinal))
            {
                yield return $"{line + 1}: {text}";
            }
        }
    }

    /// <summary>"line: text" for each text attribute of the view that isn't bound, a resource or a plain name.</summary>
    public static IEnumerable<string> InAxaml(string source)
    {
        var view = XDocument.Parse(source, LoadOptions.SetLineInfo);
        foreach (var attribute in view.Descendants().Attributes())
        {
            if (TextAttributes.Contains(attribute.Name.LocalName) && IsShownText(attribute.Value))
            {
                yield return $"{((System.Xml.IXmlLineInfo)attribute).LineNumber}: {attribute.Value}";
            }
        }

        foreach (var setter in view.Descendants().Where(element => element.Name.LocalName == "Setter"))
        {
            if (TextAttributes.Contains((string?)setter.Attribute("Property") ?? string.Empty) && (string?)setter.Attribute("Value") is { } value
                && IsShownText(value))
            {
                yield return $"{((System.Xml.IXmlLineInfo)setter).LineNumber}: {value}";
            }
        }

        foreach (var element in view.Descendants().Where(element => !element.HasElements && !element.Name.LocalName.Contains('.')))
        {
            if (IsShownText(element.Value) && LooksGerman(element.Value))
            {
                yield return $"{((System.Xml.IXmlLineInfo)element).LineNumber}: {element.Value.Trim()}";
            }
        }
    }

    private static bool IsShownText(string value) =>
        !value.TrimStart().StartsWith('{') && value.Any(char.IsLetter) && !NotTranslated.Contains(value.Trim());

    private static bool IsDiagnostic(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case AttributeSyntax:
                    return true;
                case BaseObjectCreationExpressionSyntax creation when IsException(creation):
                    return true;
                case InvocationExpressionSyntax call when IsLogCall(call):
                    return true;
            }
        }

        return false;
    }

    private static bool IsException(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax named => named.Type.ToString().EndsWith("Exception", StringComparison.Ordinal),
        _ => creation.Parent is ThrowExpressionSyntax or ThrowStatementSyntax,
    };

    private static bool IsLogCall(InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax name => LogCalls.Contains(name.Identifier.Text),
        MemberAccessExpressionSyntax { Name.Identifier.Text: "Write" } member => member.Expression.ToString().EndsWith("AppLog", StringComparison.Ordinal),
        MemberAccessExpressionSyntax { Name.Identifier.Text: "Invoke" } member => IsLogName(member.Expression.ToString()),
        MemberAccessExpressionSyntax member => LogCalls.Contains(member.Name.Identifier.Text),
        MemberBindingExpressionSyntax { Name.Identifier.Text: "Invoke" } => call.Parent is ConditionalAccessExpressionSyntax access && IsLogName(access.Expression.ToString()),
        _ => false,
    };

    private static bool IsLogName(string name) =>
        name.TrimStart('_').StartsWith("log", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Log", StringComparison.Ordinal)
        || name.TrimStart('_').StartsWith("warn", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("[äöüÄÖÜß„“‚‘]")]
    private static partial Regex GermanMarks();

    [GeneratedRegex(@"\b(?:der|das|den|dem|des|ein|eine|einen|einem|einer|und|oder|nicht|kein|keine|ist|sind|wird|werden|wurde|mit|für|auf|bei|von|zum|zur|im|nach|noch|schon|jetzt|alle|alles|neu|bitte|hier|aus|spiel|spiele|starten|gestartet|beenden|beendet|abbrechen|weiter|auswählen|installieren|installiert|deinstallieren|aktualisieren|suchen|suche|einstellungen|bibliothek|fertig|laden|gefunden|fehlt|fehler|hilfe|konto|anmelden|abmelden|ja|nein|ansicht|sortierung|seite|tastatur|treffer|wählen|erneut|versuchen|umschalten|wechseln|eingeben|verwerfen|wiederherstellen|entfernen|einlesen|eingelesen|launchern|gespielt|zuletzt|neueste|älteste|mausmodus|vorgabe|weitere|startet|wartet|pausiert|meldung|meldungen|warnung|warnungen|dringlichkeit|heute|gestern|vor|tagen|wochen|monaten|std|uhr|min|laufwerk|relevanz|typ|wert|werte|kategorie|mehrere|aktiv|ordner|bearbeiten|verschieben|zuordnen|beheben|behoben|erlaubt|deaktiviert|automatisch|individuell|leeren|ausblenden|umschalt|leertaste|stand|bilder|sekunde|sekunden|halten|laut|standard|standardlogo|installationen|installationsdatum|haltedauer|zeigergeschwindigkeit|mauszeiger|pfeiltasten|werkzeug|werkzeuge|abrocken|spielen|anmeldung|herunterladen|abstecken|einzelner|infotext|eingabe|ort|grenzen|betroffen|allgemein|speicherort|reihenfolge|steuerung)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GermanWord();
}
