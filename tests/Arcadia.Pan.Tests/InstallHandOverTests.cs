using System.Text;

namespace Arcadia.Pan.Tests;

/// <summary>
/// The hand-over of a driver install (#413, D7 option A): elevated Pan waits for Arcadia's one message and installs only
/// what the command line announced, from the run's folder; it ends without changing anything on <c>abort</c>, a gone
/// Arcadia or its timeout.
/// </summary>
public sealed class InstallHandOverTests : IDisposable
{
    private static readonly string Hash = new('a', 64);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ArcadiaPanTests", "handover-" + Guid.NewGuid().ToString("N"));

    public InstallHandOverTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private InstallRun Run(bool usbip = true, bool hidHide = true) => new(InstallRun.NewId(), 4242, _folder, usbip, hidHide, HidHideRepair: false);

    private string InFolder(string name) => Path.Combine(_folder, name);

    private static HandOverResult Read(string text, InstallRun run, Func<string, bool>? isReparsePoint = null) =>
        InstallHandOver.Read(Encoding.UTF8.GetBytes(text), run, isReparsePoint ?? (_ => false));

    [Fact]
    [Trait("TestCase", "T413-26")]
    public void A_go_with_the_announced_installers_from_the_run_s_folder_is_taken()
    {
        var run = Run();
        var installers = new HandedOverInstallers(InFolder("USBip-x64.exe"), Hash, InFolder("HidHide.exe"));

        var result = InstallHandOver.Read(InstallHandOver.Go(installers), run, _ => false);

        Assert.Equal(HandOverEnd.Go, result.End);
        Assert.Equal(installers, result.Installers);
    }

    [Fact]
    [Trait("TestCase", "T413-17")]
    public void Abort_ends_the_run()
    {
        Assert.Equal(HandOverEnd.Abort, InstallHandOver.Read(InstallHandOver.Abort(), Run(), _ => false).End);
    }

    [Theory]
    [Trait("TestCase", "T413-26")]
    [InlineData("usbip-not-announced")]
    [InlineData("hidhide-missing")]
    [InlineData("hidhide-hash")]
    [InlineData("outside")]
    [InlineData("dot-dot")]
    [InlineData("relative")]
    [InlineData("no-hash")]
    [InlineData("bad-hash")]
    [InlineData("unknown-line")]
    [InlineData("repeated")]
    [InlineData("no-go")]
    [InlineData("no-line-break")]
    public void A_go_that_doesn_t_match_the_command_line_is_refused(string kind)
    {
        var run = Run(usbip: kind != "usbip-not-announced");
        var usbip = InFolder("USBip-x64.exe");
        var hidHide = InFolder("HidHide.exe");
        var text = kind switch
        {
            "usbip-not-announced" => $"go\nusbip={usbip}\nusbip-sha256={Hash}\nhidhide={hidHide}\n",
            "hidhide-missing" => $"go\nusbip={usbip}\nusbip-sha256={Hash}\n",
            // A hash for HidHide can't replace the pinned one: the key doesn't exist.
            "hidhide-hash" => $"go\nusbip={usbip}\nusbip-sha256={Hash}\nhidhide={hidHide}\nhidhide-sha256={Hash}\n",
            "outside" => $"go\nusbip={Path.Combine(Path.GetTempPath(), "USBip-x64.exe")}\nusbip-sha256={Hash}\nhidhide={hidHide}\n",
            "dot-dot" => $"go\nusbip={_folder}{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}{Path.GetFileName(_folder)}{Path.DirectorySeparatorChar}USBip-x64.exe\nusbip-sha256={Hash}\nhidhide={hidHide}\n",
            "relative" => $"go\nusbip=USBip-x64.exe\nusbip-sha256={Hash}\nhidhide={hidHide}\n",
            "no-hash" => $"go\nusbip={usbip}\nhidhide={hidHide}\n",
            "bad-hash" => $"go\nusbip={usbip}\nusbip-sha256=xyz\nhidhide={hidHide}\n",
            "unknown-line" => $"go\nusbip={usbip}\nusbip-sha256={Hash}\nhidhide={hidHide}\nrun=other.exe\n",
            "repeated" => $"go\nusbip={usbip}\nusbip={usbip}\nusbip-sha256={Hash}\nhidhide={hidHide}\n",
            "no-go" => $"usbip={usbip}\nusbip-sha256={Hash}\nhidhide={hidHide}\n",
            _ => $"go\nusbip={usbip}\nusbip-sha256={Hash}\nhidhide={hidHide}",
        };

        var result = Read(text, run);

        Assert.Equal(HandOverEnd.Refused, result.End);
        Assert.Null(result.Installers);
        Assert.False(string.IsNullOrEmpty(result.Reason));
    }

    [Fact]
    [Trait("TestCase", "T413-26")]
    public void A_message_over_4_KB_is_refused()
    {
        var text = $"go\nusbip={InFolder(new string('a', 4200) + ".exe")}\nusbip-sha256={Hash}\nhidhide={InFolder("HidHide.exe")}\n";

        Assert.Equal(HandOverEnd.Refused, Read(text, Run()).End);
    }

    [Theory]
    [Trait("TestCase", "T413-26")]
    [InlineData(true)]
    [InlineData(false)]
    public void An_installer_through_a_link_or_junction_is_refused(bool folderIsLink)
    {
        var usbip = InFolder("USBip-x64.exe");
        var text = $"go\nusbip={usbip}\nusbip-sha256={Hash}\nhidhide={InFolder("HidHide.exe")}\n";

        var result = Read(text, Run(), path => folderIsLink ? path == _folder : path == usbip);

        Assert.Equal(HandOverEnd.Refused, result.End);
    }

    [Fact]
    [Trait("TestCase", "T413-17")]
    public async Task Arcadia_s_end_before_the_hand_over_ends_the_wait_at_once()
    {
        var channel = new FakeChannel();
        var wait = InstallHandOver.WaitAsync(channel, Run(), TimeSpan.FromMinutes(30), TimeProvider.System, _ => false);

        channel.Gone.SetResult();

        Assert.Equal(HandOverEnd.ArcadiaGone, (await wait.WaitAsync(TimeSpan.FromSeconds(10))).End);
    }

    [Fact]
    [Trait("TestCase", "T413-17")]
    public async Task A_pipe_that_breaks_without_a_message_counts_as_Arcadia_gone()
    {
        var channel = new FakeChannel();
        channel.Message.SetResult(null);

        var result = await InstallHandOver.WaitAsync(channel, Run(), TimeSpan.FromMinutes(30), TimeProvider.System, _ => false);

        Assert.Equal(HandOverEnd.ArcadiaGone, result.End);
    }

    [Fact]
    [Trait("TestCase", "T413-17")]
    public async Task A_pipe_that_refuses_the_read_counts_as_Arcadia_gone()
    {
        var channel = new FakeChannel();
        channel.Message.SetException(new UnauthorizedAccessException());

        var result = await InstallHandOver.WaitAsync(channel, Run(), TimeSpan.FromMinutes(30), TimeProvider.System, _ => false);

        Assert.Equal(HandOverEnd.ArcadiaGone, result.End);
    }

    [Fact]
    [Trait("TestCase", "T413-17")]
    public async Task No_go_within_the_hand_over_time_ends_the_wait()
    {
        var channel = new FakeChannel();

        var result = await InstallHandOver.WaitAsync(channel, Run(), TimeSpan.FromMilliseconds(50), TimeProvider.System, _ => false);

        Assert.Equal(HandOverEnd.Timeout, result.End);
        Assert.True(channel.ReceiveCancelled, "the read is cancelled, so the pipe can close");
    }

    [Fact]
    [Trait("TestCase", "T413-17")]
    public async Task An_abort_read_in_full_wins_over_Arcadia_closing_right_after()
    {
        var channel = new FakeChannel();
        channel.Message.SetResult(InstallHandOver.Abort());
        channel.Gone.SetResult();

        var result = await InstallHandOver.WaitAsync(channel, Run(), TimeSpan.FromMinutes(30), TimeProvider.System, _ => false);

        Assert.True(result.End is HandOverEnd.Abort or HandOverEnd.ArcadiaGone, "either way nothing is installed");
        Assert.NotEqual(HandOverEnd.Go, result.End);
    }

    [Fact]
    [Trait("TestCase", "T413-17")]
    public void Pan_waits_longer_than_a_minute_but_not_forever()
    {
        Assert.InRange(PanTimeouts.HandOver, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
    }

    private sealed class FakeChannel : IHandOverChannel
    {
        public TaskCompletionSource Gone { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<byte[]?> Message { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ReceiveCancelled { get; private set; }

        public Task ClientGone => Gone.Task;

        public async Task<byte[]?> ReceiveAsync(int maxBytes, CancellationToken cancellationToken)
        {
            try
            {
                return await Message.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ReceiveCancelled = true;
                throw;
            }
        }
    }
}
