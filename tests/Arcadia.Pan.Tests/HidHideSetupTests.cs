using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Arcadia.Pan.Tests;

/// <summary>
/// HidHide's setup inside Pan's <c>--install</c> and its removal at Arcadia's uninstall (#243 part B2): which
/// installer is trusted, how a setup that hangs at its last step is judged, and which uninstallers Pan may run.
/// </summary>
public sealed class HidHideSetupTests
{
    [Theory]
    [Trait("TestCase", "T243-32")]
    [InlineData(0, "1.5.230", HidHideSetupOutcome.Installed)]
    [InlineData(3010, "1.5.230", HidHideSetupOutcome.InstalledRestartPending)]
    [InlineData(null, "1.5.230", HidHideSetupOutcome.InstalledSetupStillRunning)]
    [InlineData(null, null, HidHideSetupOutcome.Failed)]
    [InlineData(0, null, HidHideSetupOutcome.Failed)]
    [InlineData(1603, "1.5.230", HidHideSetupOutcome.Failed)]
    [InlineData(0, "1.1.0", HidHideSetupOutcome.Failed)]
    [InlineData(null, "2.0.0", HidHideSetupOutcome.Failed)]
    public void A_setup_counts_as_done_only_with_a_supported_driver_in_place(int? exitCode, string? version, HidHideSetupOutcome expected)
    {
        Assert.Equal(expected, HidHideSetup.Judge(exitCode, version is null ? null : Version.Parse(version)));
    }

    [Theory]
    [InlineData("1.2", true)]
    [InlineData("1.5.230.0", true)]
    [InlineData("1.99", true)]
    [InlineData("1.1.999", false)]
    [InlineData("2.0", false)]
    [InlineData("0.9", false)]
    public void Pan_supports_HidHide_from_1_2_up_to_the_next_major(string version, bool supported)
    {
        Assert.Equal(supported, HidHideRelease.IsSupported(Version.Parse(version)));
    }

    [Fact]
    public void The_pinned_installer_is_a_version_Pan_supports()
    {
        Assert.True(HidHideRelease.IsSupported(HidHideRelease.Version));
        Assert.EndsWith(HidHideRelease.InstallerName, HidHideRelease.InstallerUrl.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(64, HidHideRelease.Sha256.Length);
    }

    /// <summary>The subject of the certificate that signed HidHide 1.5.230 (read from the installer, 2026-10-27).</summary>
    [Fact]
    [Trait("TestCase", "T243-32")]
    public void The_publisher_pin_matches_the_certificate_HidHide_is_signed_with_and_nothing_else()
    {
        const string nefarius = "CN=Nefarius Software Solutions e.U., O=Nefarius Software Solutions e.U., L=Wels, C=AT, SERIALNUMBER=505039f, OID.2.5.4.15=Private Organization, OID.1.3.6.1.4.1.311.60.2.1.3=AT";
        Assert.True(HidHideRelease.Publisher.Matches(Certificate(nefarius)));

        Assert.False(HidHideRelease.Publisher.Matches(Certificate(nefarius.Replace("505039f", "505039g", StringComparison.Ordinal))));
        Assert.False(HidHideRelease.Publisher.Matches(Certificate(nefarius.Replace("C=AT", "C=DE", StringComparison.Ordinal))));
        Assert.False(HidHideRelease.Publisher.Matches(Certificate("CN=Nefarius Software Solutions e.U., O=Someone Else, C=AT, SERIALNUMBER=505039f")));
        Assert.False(UsbipRelease.Publisher.Matches(Certificate(nefarius)));
    }

    private static X509Certificate2 Certificate(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static readonly string ProgramFiles = Path.Combine(Path.GetTempPath(), "Program Files");

    private const string HidHideCode = "{6F2A8D0B-1C3E-4F5A-9B7C-2D4E6F8A0B1C}";

    [Fact]
    [Trait("TestCase", "T243-38")]
    public void HidHide_goes_by_its_product_code_through_msiexec_never_by_its_uninstall_string()
    {
        UninstallEntry[] entries =
        [
            new("HidHide", "HidHide", "Nefarius Software Solutions e.U.", @"C:\ProgramData\Caphyon\Advanced Installer\x\HidHide_x64.exe /x", WindowsInstaller: false),
            new(HidHideCode, "HidHide", "Nefarius Software Solutions e.U.", $"MsiExec.exe /X{HidHideCode}", WindowsInstaller: true),
        ];

        var command = DriverUninstall.ForHidHide(entries, @"C:\Windows\System32");

        Assert.NotNull(command);
        Assert.Equal(Path.Combine(@"C:\Windows\System32", "msiexec.exe"), command.File);
        Assert.Equal(["/x", HidHideCode, "/qn", "/norestart"], command.Arguments);
    }

    [Fact]
    [Trait("TestCase", "T243-38")]
    public void Without_an_MSI_entry_of_HidHide_Pan_runs_nothing_for_it()
    {
        UninstallEntry[] entries =
        [
            new("HidHide", "HidHide", "Nefarius Software Solutions e.U.", @"C:\x\HidHide_x64.exe /x", WindowsInstaller: false),
            new("{not-a-guid}", "HidHide", null, "MsiExec.exe /X{not-a-guid}", WindowsInstaller: true),
            new(HidHideCode, "Something else", null, $"MsiExec.exe /X{HidHideCode}", WindowsInstaller: true),
            new("{11111111-2222-3333-4444-555555555555}", "HidHide Companion", "Someone else", "MsiExec.exe /X{11111111-2222-3333-4444-555555555555}", WindowsInstaller: true),
        ];

        Assert.Null(DriverUninstall.ForHidHide(entries, @"C:\Windows\System32"));
    }

    [Fact]
    [Trait("TestCase", "T243-38")]
    public void Usbip_goes_by_its_own_uninstaller_from_Program_Files_silently()
    {
        var uninstaller = Path.Combine(ProgramFiles, "USBip", "unins000.exe");
        UninstallEntry[] entries = [new("USBip_is1", "USBip 0.9.8.1", "vadimgrn", $"\"{uninstaller}\"", WindowsInstaller: false)];

        var command = DriverUninstall.ForUsbip(entries, ProgramFiles);

        Assert.NotNull(command);
        Assert.Equal(uninstaller, command.File);
        Assert.Equal(["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART"], command.Arguments);
    }

    [Theory]
    [Trait("TestCase", "T243-38")]
    [InlineData("elsewhere")]
    [InlineData("other-exe")]
    [InlineData("relative")]
    [InlineData("traversal")]
    public void An_uninstall_string_that_is_not_usbip_s_own_uninstaller_in_Program_Files_is_not_run(string kind)
    {
        var command = kind switch
        {
            "elsewhere" => $"\"{Path.Combine(Path.GetTempPath(), "Users", "x", "unins000.exe")}\"",
            "other-exe" => $"\"{Path.Combine(ProgramFiles, "USBip", "cmd.exe")}\" /c del",
            "relative" => "unins000.exe",
            _ => $"\"{Path.Combine(ProgramFiles, "..", "Evil", "unins000.exe")}\"",
        };
        UninstallEntry[] entries = [new("USBip_is1", "USBip 0.9.8.1", null, command, WindowsInstaller: false)];

        Assert.Null(DriverUninstall.ForUsbip(entries, ProgramFiles));
    }
}
