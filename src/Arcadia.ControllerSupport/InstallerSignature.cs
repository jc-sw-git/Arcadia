using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Who an installer must be signed by. The certificate's subject is matched by organization, country and
/// registration number, not by thumbprint or serial number: those change with every renewal, the legal
/// entity doesn't (#29).
/// </summary>
public sealed record PublisherPin(string Organization, string Country, string RegistrationNumber)
{
    private const string OrganizationOid = "2.5.4.10";
    private const string CountryOid = "2.5.4.6";
    private const string SerialNumberOid = "2.5.4.5";

    public bool Matches(X509Certificate2 signer)
    {
        var values = new Dictionary<string, string>();
        foreach (var name in signer.SubjectName.EnumerateRelativeDistinguishedNames())
        {
            // A multi-valued name could smuggle a second organization in; a certificate that uses one doesn't match.
            if (name.HasMultipleElements)
            {
                return false;
            }

            // The same attribute twice is ambiguous, so it doesn't match either.
            if (!values.TryAdd(name.GetSingleElementType().Value ?? string.Empty, name.GetSingleElementValue() ?? string.Empty))
            {
                return false;
            }
        }

        return values.GetValueOrDefault(OrganizationOid) == Organization
               && values.GetValueOrDefault(CountryOid) == Country
               && values.GetValueOrDefault(SerialNumberOid) == RegistrationNumber;
    }
}

/// <summary>What Windows says about a file's Authenticode signature; <see cref="Signer"/> only if <see cref="Status"/> is 0.</summary>
public sealed record SignatureCheck(int Status, X509Certificate2? Signer)
{
    public bool IsTrusted => Status == 0 && Signer is not null;
}

/// <summary>Checks a file's Authenticode signature (replaced in tests).</summary>
public interface IAuthenticode
{
    SignatureCheck Verify(string file);
}

/// <summary>The installer is not signed by the expected publisher; the message tells the user what to do.</summary>
public sealed class UntrustedInstallerException(string message) : InvalidOperationException(message);

public static class InstallerSignature
{
    private const int NoSignature = unchecked((int)0x800B0100);

    /// <summary>
    /// Runs <paramref name="start"/> only if <paramref name="file"/> is validly signed (chain, expiry, revocation,
    /// checked by Windows) by <paramref name="publisher"/>. The file stays open without write access meanwhile, so
    /// it can't be swapped between the check and the start of the process.
    /// </summary>
    /// <exception cref="UntrustedInstallerException">The file is unsigned, its signature is invalid or it is signed by someone else; nothing was started.</exception>
    public static async Task<T> RunIfTrustedAsync<T>(string file, string name, PublisherPin publisher, string manualInstallUrl, IAuthenticode authenticode, Func<Task<T>> start)
    {
        await using var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        var check = authenticode.Verify(file);
        var problem = check switch
        {
            { Status: NoSignature } => "Er ist nicht signiert.",
            { Status: not 0 } => $"Seine Signatur ist ungültig (Windows-Code 0x{check.Status:X8}).",
            { Signer: null } => "Sein Herausgeber ließ sich nicht lesen.",
            _ when !publisher.Matches(check.Signer) => $"Er ist nicht von {publisher.Organization} signiert.",
            _ => null,
        };
        if (problem is not null)
        {
            throw new UntrustedInstallerException(
                $"{name} wurde nicht gestartet. {problem} Installiere den Treiber bitte selbst: {manualInstallUrl}");
        }

        return await start();
    }
}

/// <summary><c>WinVerifyTrust</c>, the check Windows itself does for a signed file.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class WinTrustAuthenticode : IAuthenticode
{
    private const uint UiNone = 2;
    private const uint RevokeWholeChain = 1;
    private const uint ChoiceFile = 1;
    private const uint StateActionVerify = 1;
    private const uint StateActionClose = 2;

    // WINTRUST_ACTION_GENERIC_VERIFY_V2
    private static readonly Guid GenericVerifyV2 = new(0x00AAC56B, 0xCD44, 0x11D0, 0x8C, 0xC2, 0x00, 0xC0, 0x4F, 0xC2, 0x95, 0xEE);

    public SignatureCheck Verify(string file)
    {
        var fileInfo = new WinTrustFileInfo { Size = (uint)Marshal.SizeOf<WinTrustFileInfo>(), FilePath = Marshal.StringToHGlobalUni(file) };
        var fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        Marshal.StructureToPtr(fileInfo, fileInfoPointer, fDeleteOld: false);
        var data = new WinTrustData
        {
            Size = (uint)Marshal.SizeOf<WinTrustData>(),
            UiChoice = UiNone,
            RevocationChecks = RevokeWholeChain,
            UnionChoice = ChoiceFile,
            File = fileInfoPointer,
            StateAction = StateActionVerify,
        };
        var action = GenericVerifyV2;
        try
        {
            var status = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            data.StateAction = StateActionClose;
            WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            return status == 0 ? new SignatureCheck(0, SignerOf(file)) : new SignatureCheck(status, null);
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfo.FilePath);
            Marshal.FreeHGlobal(fileInfoPointer);
        }
    }

    // The signature is valid by now; this only reads who signed. Null if the file has no embedded signer (a catalog signature).
    private static X509Certificate2? SignerOf(string file)
    {
        try
        {
#pragma warning disable SYSLIB0057 // X509CertificateLoader has no counterpart for signed files; this reads the signer only, trust was checked by WinVerifyTrust.
            return X509CertificateLoader.LoadCertificate(X509Certificate.CreateFromSignedFile(file).GetRawCertData());
#pragma warning restore SYSLIB0057
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or IOException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [LibraryImport("wintrust.dll")]
    private static partial int WinVerifyTrust(IntPtr window, ref Guid action, ref WinTrustData data);
}
