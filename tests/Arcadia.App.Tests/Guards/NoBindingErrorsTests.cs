using Avalonia.Headless.XUnit;
using Avalonia.Logging;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// No binding of any page, dialog or settings category fails (#684): Avalonia logs a failed binding as a "[Binding]"
/// warning and shows the fallback, so nobody sees it but <c>arcadia.log</c>; a hint or text may be empty for a moment.
/// </summary>
[Collection(LocalizationSettingsTests.Name)]
public sealed class NoBindingErrorsTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T684-1")]
    public async Task No_page_dialog_or_settings_category_logs_a_binding_error()
    {
        var sink = new BindingSink();
        var before = Logger.Sink;
        Logger.Sink = sink;
        try
        {
            await GuardWalk.EveryAsync(_ => true, (arcadia, _, state) => sink.Mark(state));
        }
        finally
        {
            Logger.Sink = before;
        }

        Assert.True(sink.Errors.Count == 0, string.Join("\n", sink.Errors.Distinct()));
    }

    /// <summary>Collects the binding errors, each with the state the walk was in when it came.</summary>
    private sealed class BindingSink : ILogSink
    {
        private string _state = "Start";

        public List<string> Errors { get; } = [];

        public void Mark(string state) => _state = state;

        public bool IsEnabled(LogEventLevel level, string area) => area == LogArea.Binding && level >= LogEventLevel.Warning;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            if (IsEnabled(level, area))
            {
                Errors.Add($"{_state}: {messageTemplate}");
            }
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (IsEnabled(level, area))
            {
                Errors.Add($"{_state}: {FormatTemplate(messageTemplate, propertyValues)}");
            }
        }

        // Avalonia's templates name their values: "{Property}"; the values follow in order.
        private static string FormatTemplate(string template, object?[] values)
        {
            var next = 0;
            return System.Text.RegularExpressions.Regex.Replace(template, @"\{[^}]*\}", _ => next < values.Length ? values[next++]?.ToString() ?? "null" : "?");
        }
    }
}
