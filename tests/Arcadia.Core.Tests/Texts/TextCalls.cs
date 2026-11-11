using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Arcadia.Core.Tests.Texts;

/// <summary>
/// Finds calls of a generated text method (<c>AppTexts.Key(…)</c>) that pass two or more values without their names.
/// The parameters are in name order, so a positional call would put a value into the wrong placeholder once the order
/// of names and arguments differs; a named call stays right whatever a rewording does (#480).
/// </summary>
internal static class TextCalls
{
    /// <summary>"line: call" for each such call of one of <paramref name="textClasses"/> in the C# source.</summary>
    public static IEnumerable<string> WithoutNames(string source, IReadOnlySet<string> textClasses)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is MemberAccessExpressionSyntax { Expression: var receiver }
                && textClasses.Contains(receiver.ToString().Split('.')[^1])
                && call.ArgumentList.Arguments.Count >= 2
                && call.ArgumentList.Arguments.Any(argument => argument.NameColon is null))
            {
                yield return $"{tree.GetLineSpan(call.Span).StartLinePosition.Line + 1}: {call}";
            }
        }
    }
}
