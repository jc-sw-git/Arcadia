using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

public sealed class HealthIssuesTests
{
    private static HealthIssue Issue(HealthSeverity severity) => new($"id-{severity}", severity, "Titel", "Text");

    [Fact]
    public void No_issues_mark_nothing() => Assert.Null(HealthIssues.WorstProblem([]));

    [Fact]
    public void Information_alone_marks_nothing() =>
        Assert.Null(HealthIssues.WorstProblem([Issue(HealthSeverity.Info), Issue(HealthSeverity.Info)]));

    [Fact]
    public void A_warning_beside_information_marks_a_warning() =>
        Assert.Equal(HealthSeverity.Warning, HealthIssues.WorstProblem([Issue(HealthSeverity.Info), Issue(HealthSeverity.Warning)]));

    [Fact]
    public void An_error_outweighs_a_warning() =>
        Assert.Equal(HealthSeverity.Error, HealthIssues.WorstProblem([Issue(HealthSeverity.Error), Issue(HealthSeverity.Warning)]));
}
