using Arcadia.App.Messages;

namespace Arcadia.App.Tests;

/// <summary>The logs without the disk or the shell (#433): a test sets which exist and sees which were opened.</summary>
internal sealed class FakeLogFiles : ILogFiles
{
    public Dictionary<LogKind, LogFileState> States { get; } = new()
    {
        [LogKind.Arcadia] = LogFileState.Present,
        [LogKind.Viiper] = LogFileState.Present,
        [LogKind.Pan] = LogFileState.PanNotInstalled,
    };

    public List<LogKind> Opened { get; } = [];

    /// <summary>What <see cref="Open"/> throws, as Windows does without an app for .log or without access.</summary>
    public Exception? Failure { get; set; }

    public string ShownPathOf(LogKind kind) => $@"C:\Beispiel\{LogNames.Of(kind)}";

    public LogFileState StateOf(LogKind kind) => States[kind];

    public void Open(LogKind kind)
    {
        if (Failure is { } failure)
        {
            throw failure;
        }

        lock (Opened)
        {
            Opened.Add(kind);
        }
    }
}
