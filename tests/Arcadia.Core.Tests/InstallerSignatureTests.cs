using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Arcadia.ControllerSupport;
using Arcadia.Pan;

namespace Arcadia.Core.Tests;

/// <summary>An installer runs with administrator rights only if it is signed by the expected publisher (#29).</summary>
public sealed class InstallerSignatureTests : IDisposable
{
    private const string ManualUrl = "https://example.test/manual";
    private const int NoSignature = unchecked((int)0x800B0100);
    private const int ChainUntrusted = unchecked((int)0x800B0109);

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"arcadia-installer-{Guid.NewGuid():N}.exe");

    public InstallerSignatureTests() => File.WriteAllBytes(_file, [1, 2, 3]);

    public void Dispose() => File.Delete(_file);

    private static X509Certificate2 Certificate(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(new X500DistinguishedName(subject), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static readonly PublisherPin Pin = new("Cloudyne Systems (Scheibling Consulting AB)", "SE", "559171-6567");

    private const string RealSubject =
        "E=itsec@scheibling.se, CN=Cloudyne Systems (Scheibling Consulting AB), O=Cloudyne Systems (Scheibling Consulting AB), "
        + "L=Uppsala, C=SE, 2.5.4.5=559171-6567";

    private sealed class FakeAuthenticode(SignatureCheck result) : IAuthenticode
    {
        public int Checks { get; private set; }

        public SignatureCheck Verify(string file)
        {
            Checks++;
            return result;
        }
    }

    private async Task<(bool Started, UntrustedInstallerException? Rejected)> Run(SignatureCheck check, PublisherPin? pin = null, string? expectedSha256 = null)
    {
        var started = false;
        try
        {
            await InstallerSignature.RunIfTrustedAsync(_file, "Der Installer", pin ?? Pin, ManualUrl, new FakeAuthenticode(check), () =>
            {
                started = true;
                return Task.FromResult(true);
            }, expectedSha256);
            return (started, null);
        }
        catch (UntrustedInstallerException ex)
        {
            return (started, ex);
        }
    }

    [Fact]
    public async Task An_installer_signed_by_the_expected_publisher_starts()
    {
        using var signer = Certificate(RealSubject);

        var (started, rejected) = await Run(new SignatureCheck(0, signer));

        Assert.True(started);
        Assert.Null(rejected);
    }

    [Fact]
    [Trait("TestCase", "T243-32")]
    public async Task A_signed_installer_that_is_not_the_file_Arcadia_checked_does_not_start()
    {
        using var signer = Certificate(RealSubject);
        var other = Convert.ToHexString(SHA256.HashData([9, 9, 9]));

        var (started, rejected) = await Run(new SignatureCheck(0, signer), expectedSha256: other);

        Assert.False(started);
        Assert.Contains("SHA-256 weicht ab", rejected?.Message);
        Assert.Contains(ManualUrl, rejected?.Message);
    }

    [Fact]
    [Trait("TestCase", "T243-32")]
    public async Task A_signed_installer_with_the_hash_Arcadia_checked_starts_whatever_the_hash_s_case()
    {
        using var signer = Certificate(RealSubject);
        var same = Convert.ToHexString(SHA256.HashData([1, 2, 3])).ToLowerInvariant();

        var (started, rejected) = await Run(new SignatureCheck(0, signer), expectedSha256: same);

        Assert.True(started);
        Assert.Null(rejected);
    }

    [Fact]
    public void The_usbip_installer_is_run_with_Inno_Setup_s_silent_switches()
    {
        // Inno Setup doesn't know NSIS's "/S" and would show its wizard inside Pan's elevated, windowless step.
        Assert.Equal(["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/RESTARTEXITCODE=3010"], UsbipRelease.SilentArguments);
    }

    [Fact]
    public async Task An_unsigned_installer_does_not_start_and_the_message_names_the_manual_install()
    {
        var (started, rejected) = await Run(new SignatureCheck(NoSignature, null));

        Assert.False(started);
        Assert.NotNull(rejected);
        Assert.Contains("nicht signiert", rejected.Message);
        Assert.Contains(ManualUrl, rejected.Message);
    }

    [Fact]
    public async Task An_installer_whose_signature_Windows_rejects_does_not_start()
    {
        using var signer = Certificate(RealSubject);

        // Even with the right name in the certificate: a chain that doesn't lead to a trusted root is a self-made name.
        var (started, rejected) = await Run(new SignatureCheck(ChainUntrusted, signer));

        Assert.False(started);
        Assert.Contains("0x800B0109", rejected!.Message);
    }

    [Fact]
    public async Task An_installer_signed_by_someone_else_does_not_start()
    {
        using var signer = Certificate("CN=Someone Else, O=Someone Else AB, C=SE, 2.5.4.5=111111-1111");

        var (started, rejected) = await Run(new SignatureCheck(0, signer));

        Assert.False(started);
        Assert.Contains(Pin.Organization, rejected!.Message);
        Assert.Contains(ManualUrl, rejected.Message);
    }

    [Theory]
    [InlineData("O=Cloudyne Systems (Scheibling Consulting AB), C=SE, 2.5.4.5=000000-0000")]
    [InlineData("O=Cloudyne Systems (Scheibling Consulting AB), C=DE, 2.5.4.5=559171-6567")]
    [InlineData("O=Cloudyne Systems, C=SE, 2.5.4.5=559171-6567")]
    [InlineData("O=Cloudyne Systems (Scheibling Consulting AB), C=SE")]
    [InlineData("CN=Cloudyne Systems (Scheibling Consulting AB), C=SE, 2.5.4.5=559171-6567")]
    public async Task One_wrong_or_missing_part_of_the_publisher_is_enough_to_reject(string subject)
    {
        using var signer = Certificate(subject);

        var (started, _) = await Run(new SignatureCheck(0, signer));

        Assert.False(started);
    }

    [Fact]
    public async Task A_second_organization_in_the_subject_does_not_pass()
    {
        using var signer = Certificate("O=Evil Ltd, O=Cloudyne Systems (Scheibling Consulting AB), C=SE, 2.5.4.5=559171-6567");

        var (started, _) = await Run(new SignatureCheck(0, signer));

        Assert.False(started);
    }

    [Fact]
    public async Task A_valid_signature_without_a_readable_signer_does_not_start()
    {
        var (started, rejected) = await Run(new SignatureCheck(0, null));

        Assert.False(started);
        Assert.NotNull(rejected);
    }

    [Fact]
    public async Task A_failing_start_is_not_hidden_by_the_check()
    {
        using var signer = Certificate(RealSubject);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InstallerSignature.RunIfTrustedAsync<bool>(_file, "Der Installer", Pin, ManualUrl, new FakeAuthenticode(new SignatureCheck(0, signer)),
                () => throw new InvalidOperationException("Fehler 1")));
    }

    [Fact]
    public void The_pinned_publisher_is_the_one_checked_on_the_real_installer()
    {
        Assert.Equal(Pin, UsbipRelease.Publisher);
    }
}
