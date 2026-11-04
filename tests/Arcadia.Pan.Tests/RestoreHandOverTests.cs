using System.Text;

namespace Arcadia.Pan.Tests;

/// <summary>
/// The restore's hand-over (#471): elevated Pan restores only on Arcadia's exact <c>go</c>, which Arcadia sends once the
/// Windows prompt was accepted and it let go of Pan; <c>abort</c>, Arcadia's end or the timeout restore nothing.
/// </summary>
public sealed class RestoreHandOverTests
{
    private static readonly string Id = new('a', 32);

    [Fact]
    [Trait("TestCase", "T471-3")]
    public void A_run_on_the_command_line_is_read_and_written_back_alike()
    {
        var run = new RestoreRun(Id, 4242);

        Assert.Equal(run, RestoreRun.Parse(["--restore", .. run.ToArguments()]));
    }

    [Fact]
    [Trait("TestCase", "T471-3")]
    public void Without_a_run_the_restore_by_hand_runs_at_once()
    {
        Assert.Null(RestoreRun.Parse(["--restore"]));
    }

    [Theory]
    [Trait("TestCase", "T471-3")]
    [InlineData("--run", "ABC", "--client", "4242")]
    [InlineData("--run", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "--client", "-1")]
    [InlineData("--run", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "--client", "x")]
    [InlineData("--client", "4242")]
    public void A_half_or_malformed_run_is_refused(params string[] options)
    {
        Assert.Throws<InvalidOperationException>(() => RestoreRun.Parse(["--restore", .. options]));
    }

    [Fact]
    [Trait("TestCase", "T471-3")]
    public void Only_an_exact_go_or_abort_is_taken()
    {
        Assert.Equal(HandOverEnd.Go, RestoreHandOver.Read(RestoreHandOver.Go()).End);
        Assert.Equal(HandOverEnd.Abort, RestoreHandOver.Read(InstallHandOver.Abort()).End);
        foreach (var text in new[] { "go", "go\nusbip=C:\\x.exe\n", "GO\n", "go\n\n" })
        {
            Assert.Equal(HandOverEnd.Refused, RestoreHandOver.Read(Encoding.UTF8.GetBytes(text)).End);
        }
    }

    [Fact]
    [Trait("TestCase", "T471-3")]
    public async Task No_go_within_the_limit_restores_nothing()
    {
        var channel = new FakeChannel();

        var result = await RestoreHandOver.WaitAsync(channel, TimeSpan.FromMilliseconds(50), TimeProvider.System);

        Assert.Equal(HandOverEnd.Timeout, result.End);
    }

    [Fact]
    [Trait("TestCase", "T471-3")]
    public async Task Arcadia_s_end_before_the_go_restores_nothing()
    {
        var channel = new FakeChannel();
        var wait = RestoreHandOver.WaitAsync(channel, TimeSpan.FromMinutes(5), TimeProvider.System);

        channel.Gone.SetResult();

        Assert.Equal(HandOverEnd.ArcadiaGone, (await wait.WaitAsync(TimeSpan.FromSeconds(10))).End);
    }

    [Fact]
    [Trait("TestCase", "T471-3")]
    public async Task A_go_read_in_full_starts_the_restore()
    {
        var channel = new FakeChannel();
        channel.Message.SetResult(RestoreHandOver.Go());

        Assert.Equal(HandOverEnd.Go, (await RestoreHandOver.WaitAsync(channel, TimeSpan.FromMinutes(5), TimeProvider.System)).End);
    }

    [Fact]
    public void The_restore_s_pipe_is_not_the_install_s()
    {
        Assert.NotEqual(InstallHandOver.PipeName(Id), RestoreHandOver.PipeName(Id));
    }

    private sealed class FakeChannel : IHandOverChannel
    {
        public TaskCompletionSource Gone { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<byte[]?> Message { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ClientGone => Gone.Task;

        public Task<byte[]?> ReceiveAsync(int maxBytes, CancellationToken cancellationToken) => Message.Task.WaitAsync(cancellationToken);
    }
}
