namespace Arcadia.Pan;

/// <summary>
/// Which access rights on Pan's data folder let somebody other than SYSTEM and administrators change what the service
/// reads and writes there (the journal, the log, the installers it runs). Reading and listing are fine: users may read
/// the log (owner, 2026-10-27). Kept free of Windows so the mask is tested everywhere.
/// </summary>
public static class DataFolderRights
{
    private const uint WriteData = 0x2;
    private const uint AppendData = 0x4;
    private const uint WriteExtendedAttributes = 0x10;
    private const uint DeleteChild = 0x40;
    private const uint WriteAttributes = 0x100;
    private const uint Delete = 0x10000;
    private const uint WriteDac = 0x40000;
    private const uint WriteOwner = 0x80000;
    private const uint GenericAll = 0x10000000;
    private const uint GenericWrite = 0x40000000;

    private const uint Writing = WriteData | AppendData | WriteExtendedAttributes | DeleteChild | WriteAttributes | Delete
        | WriteDac | WriteOwner | GenericAll | GenericWrite;

    /// <param name="mask">An access rule's rights (<c>FileSystemRights</c>, generic bits included).</param>
    public static bool Writes(uint mask) => (mask & Writing) != 0;
}
