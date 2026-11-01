using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Arcadia.Pan.Windows;

namespace Arcadia.Pan.Tests;

/// <summary>Windows with administrator rights: only an administrator can give a folder to Administrators or create a link.</summary>
public sealed class WindowsAdminFactAttribute : FactAttribute
{
    public WindowsAdminFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows only";
        }
        else if (!IsElevated())
        {
            Skip = "needs administrator rights";
        }
    }

    private static bool IsElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

/// <summary>
/// The service's DACL as Pan sets it (PAN-13): the end-to-end <see cref="ServiceTests"/> only run on the CI runner,
/// this pins what each account may do wherever Windows runs the tests.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServiceSddlTests
{
    // Service access rights (winsvc.h) as the SDDL letters spell them.
    private const int ChangeConfig = 0x0002;   // DC
    private const int QueryStatus = 0x0004;    // LC
    private const int Start = 0x0010;          // RP
    private const int Stop = 0x0020;           // WP
    private const int PauseContinue = 0x0040;  // DT
    private const int Interrogate = 0x0080;    // LO
    private const int Delete = 0x10000;        // SD
    private const int ReadControl = 0x20000;   // RC
    private const int WriteDac = 0x40000;      // WD
    private const int WriteOwner = 0x80000;    // WO

    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier Interactive = new(WellKnownSidType.InteractiveSid, null);

    private static Dictionary<SecurityIdentifier, int> Grants() =>
        new RawSecurityDescriptor(ServiceSetup.ServiceSddl).DiscretionaryAcl!.Cast<CommonAce>()
            .Where(ace => ace.AceQualifier == AceQualifier.AccessAllowed)
            .ToDictionary(ace => ace.SecurityIdentifier, ace => ace.AccessMask);

    [WindowsFact]
    public void Only_SYSTEM_administrators_and_the_interactive_user_are_named()
    {
        var grants = Grants();

        Assert.Equal(3, grants.Count);
        Assert.Contains(LocalSystem, grants.Keys);
        Assert.Contains(Administrators, grants.Keys);
        Assert.Contains(Interactive, grants.Keys);
    }

    [WindowsFact]
    public void The_interactive_user_may_start_and_query_Pan_but_never_stop_or_change_it()
    {
        var user = Grants()[Interactive];

        // Starting is how Arcadia brings Pan up; stopping would end the guard while a controller is hidden.
        Assert.Equal(Start | QueryStatus | Interrogate | ReadControl, user);
        Assert.Equal(0, user & (Stop | PauseContinue | ChangeConfig | Delete | WriteDac | WriteOwner));
    }

    [WindowsFact]
    public void Only_administrators_may_change_delete_or_take_over_the_service()
    {
        var grants = Grants();
        const int takeOver = ChangeConfig | Delete | WriteDac | WriteOwner;

        Assert.Equal(takeOver, grants[Administrators] & takeOver);
        // SYSTEM runs it and may stop it (the service's own stop), not rewrite it.
        Assert.Equal(0, grants[LocalSystem] & takeOver);
        Assert.Equal(Stop, grants[LocalSystem] & Stop);
    }
}

/// <summary>
/// <see cref="ServiceSetup.IsOurs"/> and <see cref="ServiceSetup.MoveAside"/> on temporary folders (PAN-13): what makes
/// <c>%ProgramData%\Arcadia</c> trusted, and that a folder that isn't is set aside without losing what is in it.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DataFolderTrustTests : IDisposable
{
    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-pan-trust", Guid.NewGuid().ToString("N"));

    public DataFolderTrustTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The shape <see cref="ServiceSetup.SecureDataDirectory"/> gives the data folders, with <paramref name="usersRights"/> for Users.</summary>
    private string Folder(string name, FileSystemRights usersRights = FileSystemRights.ReadAndExecute, SecurityIdentifier? owner = null)
    {
        var path = Path.Combine(_root, name);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(Users, usersRights, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.SetOwner(owner ?? Administrators);
        new DirectoryInfo(path).Create(security);
        return path;
    }

    [WindowsAdminFact]
    public void An_admin_owned_folder_with_its_own_rules_that_users_may_only_read_is_ours()
    {
        Assert.True(ServiceSetup.IsOurs(Folder("ours"), out var problem), problem);
    }

    [WindowsAdminFact]
    public void A_folder_owned_by_a_user_is_not_ours_even_with_the_right_rules()
    {
        // Its owner may rewrite the rules at any time: a folder somebody created before Pan must not be trusted.
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User!;

        Assert.False(ServiceSetup.IsOurs(Folder("user-owned", owner: user), out var problem));
        Assert.Equal($"is owned by {user}", problem);
    }

    [WindowsAdminFact]
    public void A_folder_users_may_write_to_is_not_ours()
    {
        Assert.False(ServiceSetup.IsOurs(Folder("writable", FileSystemRights.ReadAndExecute | FileSystemRights.Write), out var problem));
        Assert.Equal($"lets {Users} write", problem);
    }

    [WindowsAdminFact]
    public void A_folder_that_inherits_its_parents_rules_is_not_ours()
    {
        // Our folder with the protection taken off: the temp folder's rules flow in, and they would let its owner write.
        var info = new DirectoryInfo(Folder("inherits"));
        var security = info.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
        info.SetAccessControl(security);

        Assert.False(ServiceSetup.IsOurs(info.FullName, out var problem));
        Assert.Equal("inherits access rules", problem);
    }

    [WindowsAdminFact]
    public void A_link_to_a_folder_that_is_ours_is_not_ours_itself()
    {
        var target = Folder("target");
        var link = Path.Combine(_root, "link");
        Directory.CreateSymbolicLink(link, target);

        Assert.False(ServiceSetup.IsOurs(link, out var problem));
        Assert.Equal("is a junction or symbolic link", problem);
    }

    [WindowsAdminFact]
    public void A_missing_folder_or_a_file_in_its_place_is_not_ours()
    {
        var file = Path.Combine(_root, "file");
        File.WriteAllText(file, "x");

        Assert.False(ServiceSetup.IsOurs(Path.Combine(_root, "missing"), out var missing));
        Assert.False(ServiceSetup.IsOurs(file, out var notAFolder));
        Assert.Equal("is not a folder", missing);
        Assert.Equal("is not a folder", notAFolder);
    }

    [WindowsAdminFact]
    public void A_link_is_set_aside_by_deleting_the_link_alone()
    {
        var target = Folder("target");
        File.WriteAllText(Path.Combine(target, "keep.txt"), "x");
        var link = Path.Combine(_root, "link");
        Directory.CreateSymbolicLink(link, target);

        Assert.Equal("deleted (a link)", ServiceSetup.MoveAside(link));

        Assert.False(Directory.Exists(link));
        // An elevated delete through the link would have taken the target's files with it.
        Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
    }

    [WindowsAdminFact]
    public void A_foreign_folder_is_renamed_with_its_content_and_the_name_is_free_again()
    {
        var foreign = Path.Combine(_root, "Arcadia");
        Directory.CreateDirectory(foreign);
        File.WriteAllText(Path.Combine(foreign, "users-file.txt"), "x");

        var aside = ServiceSetup.MoveAside(foreign);

        Assert.StartsWith(foreign + ".untrusted-", aside);
        Assert.False(Directory.Exists(foreign));
        Assert.True(File.Exists(Path.Combine(aside, "users-file.txt")));
    }
}

/// <summary>
/// <see cref="PipeServer"/> on the real pipe name (PAN-13), so only where <see cref="ServiceFactAttribute"/> allows it:
/// a developer's running Pan holds that name.
/// </summary>
[SupportedOSPlatform("windows")]
[Collection(MachineStateCollection.Name)]
public sealed class PipeServerTests : IDisposable
{
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(10);
    private readonly TempJournal _journal = new();
    private readonly List<string> _log = [];
    private readonly CancellationTokenSource _stop = new();

    public void Dispose()
    {
        _stop.Dispose();
        _journal.Dispose();
    }

    private PipeServer NewServer() =>
        new(new Guardian(new FakeClock(), _journal.File, new FakeCleanup(), Log), Log);

    private void Log(string line)
    {
        lock (_log)
        {
            _log.Add(line);
        }
    }

    private string Logged()
    {
        lock (_log)
        {
            return string.Join("\n", _log);
        }
    }

    private static async Task<NamedPipeClientStream> ConnectAsync()
    {
        var client = new NamedPipeClientStream(".", PanPaths.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync((int)ReplyTimeout.TotalMilliseconds);
        return client;
    }

    private static async Task<PanMessage> ReadAsync(Stream pipe)
    {
        var buffer = new byte[PanMessage.Size];
        await pipe.ReadExactlyAsync(buffer).AsTask().WaitAsync(ReplyTimeout);
        return PanMessage.Read(buffer);
    }

    [ServiceFact]
    public async Task A_process_that_holds_the_pipe_name_first_is_never_served_beside()
    {
        using var squatter = new NamedPipeServerStream(PanPaths.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances);
        var running = NewServer().RunAsync(_stop.Token);

        await Task.Run(async () =>
        {
            while (!Logged().Contains("Pipe not created"))
            {
                await Task.Delay(20);
            }
        }).WaitAsync(ReplyTimeout);

        Assert.Contains("is another program squatting", Logged());
        await _stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    [ServiceFact]
    public async Task A_client_is_known_from_its_process_and_answered_on_the_pipe()
    {
        // Who connected is read from the pipe and the process, never from a message: a client outside the console
        // session (the CI runner) is refused for that at once, one inside it is registered and gets its hello answered.
        // A second client side by side is not tested here: only SYSTEM (the service) and the interactive user may open
        // a further instance of the pipe, and the CI runner is neither.
        var running = NewServer().RunAsync(_stop.Token);
        try
        {
            await using var client = await ConnectAsync();
            try
            {
                await client.WriteAsync(PanMessage.Of(PanCommand.Hello).ToBytes());
            }
            catch (IOException)
            {
                // Refused at the connect: Pan wrote its answer and closed the pipe before the hello arrived.
            }

            var reply = await ReadAsync(client);

            Assert.True(
                reply == PanMessage.Of(PanReply.Refused, reason: PanReason.ForeignSession) || reply.Reply == PanReply.HelloOk,
                $"unexpected reply {reply}; Pan's log:\n{Logged()}");
            Assert.Contains("Client ", Logged());
        }
        finally
        {
            await _stop.CancelAsync();
            try
            {
                await running.WaitAsync(ReplyTimeout);
            }
            catch (OperationCanceledException)
            {
                // Cancelled while waiting to create the next instance, which only SYSTEM or the interactive user may.
            }
        }
    }
}
