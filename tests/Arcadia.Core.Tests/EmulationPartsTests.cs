using Arcadia.ControllerSupport;
using Arcadia.Pan;

namespace Arcadia.Core.Tests;

/// <summary>The rows of the controller emulation's parts below "Steam Controller Kompatibilität" (#243 part B2).</summary>
public sealed class EmulationPartsTests
{
    private static HealthIssue Row(string id) => new(id, HealthSeverity.Warning, id, id, HealthFix.InstallDrivers, "Installieren");

    [Fact]
    [Trait("TestCase", "T243-33")]
    public void The_rows_stand_in_the_order_usbip_VIIPER_Pan_HidHide_and_name_the_missing_parts()
    {
        var issues = EmulationParts.InRowOrder([Row("hidhide-missing"), Row("steam-stopped"), Row("pan-missing"), Row("viiper-missing"), Row("usbip-outdated")]);

        Assert.Equal(["usbip-outdated", "viiper-missing", "pan-missing", "hidhide-missing", "steam-stopped"], issues.Select(i => i.Id));
        Assert.Equal(["usbip-Treiber", "VIIPER", "Wächterdienst Pan", "HidHide"], EmulationParts.MissingNames(issues));
        Assert.True(EmulationParts.AllMissing(issues));
    }

    [Fact]
    [Trait("TestCase", "T243-33")]
    public void A_state_of_HidHide_is_no_missing_part()
    {
        IReadOnlyList<HealthIssue> issues = [Row("usbip-missing"), Row("viiper-missing"), Row("pan-missing"), EmulationParts.FromHidingCheck(PanReason.HidHideBusy)!];

        Assert.Equal(["usbip-Treiber", "VIIPER", "Wächterdienst Pan"], EmulationParts.MissingNames(issues));
        Assert.False(EmulationParts.AllMissing(issues));
        Assert.False(EmulationParts.IsMissingPart(issues[^1]));
    }

    [Theory]
    [Trait("TestCase", "T243-35")]
    [InlineData(PanReason.HidHideMissing, "hidhide-inactive")]
    [InlineData(PanReason.HidHideUnsupported, "hidhide-interface")]
    [InlineData(PanReason.HidHideInverseMode, "hidhide-inverse")]
    [InlineData(PanReason.HidHideCloakingOffWithForeignEntries, "hidhide-cloaking-off")]
    [InlineData(PanReason.HidHideBusy, "hidhide-busy")]
    [InlineData(PanReason.None, null)]
    [InlineData(PanReason.HidingFailed, null)]
    [InlineData(null, null)]
    public void Pan_s_answer_about_hiding_maps_to_a_row_that_says_what_to_do(PanReason? reason, string? id)
    {
        var issue = EmulationParts.FromHidingCheck(reason);

        Assert.Equal(id, issue?.Id);
        if (issue is not null)
        {
            Assert.Equal(HealthSeverity.Warning, issue.Severity);
            Assert.Equal(HealthFix.None, issue.Fix);
        }
    }

    [Theory]
    [Trait("TestCase", "T243-34")]
    [InlineData(null, "hidhide-missing", "Installieren")]
    [InlineData("1.1.0.0", "hidhide-unsupported", "Aktualisieren")]
    [InlineData("2.0.0.0", "hidhide-unsupported", null)]
    [InlineData("1.2.93.0", "hidhide-unsupported", "Aktualisieren")]
    [InlineData("1.5.230.0", null, null)]
    public void HidHide_s_row_follows_its_version(string? version, string? id, string? fixLabel)
    {
        var issue = ControllerSupportPlatform.CheckHidHide(version is null ? null : Version.Parse(version));

        Assert.Equal(id, issue?.Id);
        Assert.Equal(fixLabel, issue?.FixLabel);
    }
}
