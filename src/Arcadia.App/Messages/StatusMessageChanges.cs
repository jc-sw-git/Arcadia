using Arcadia.ControllerSupport;

namespace Arcadia.App.Messages;

/// <summary>A status message as the header shows it, by a key that stays the same while it is shown.</summary>
public sealed record StatusMessage(string Id, HealthSeverity Level, string Text);

/// <summary>
/// Turns the status messages shown now into entries of <see cref="RecentMessages"/> (#433): one when a message appears,
/// one when its level changes, and "Behoben: …" with the level of the message it ends when it goes. A refresh that
/// changes nothing adds nothing. Status messages are states, so only their changes are events.
/// </summary>
public sealed class StatusMessageChanges(RecentMessages messages)
{
    public const string ResolvedPrefix = "Behoben: ";

    private readonly object _gate = new();
    private Dictionary<string, StatusMessage> _shown = [];

    public void Show(IEnumerable<StatusMessage> current)
    {
        // Under the gate, so two threads reporting at once add their entries in the order they were seen.
        lock (_gate)
        {
            var now = new Dictionary<string, StatusMessage>();
            foreach (var message in current)
            {
                now.TryAdd(message.Id, message);
            }

            foreach (var (id, message) in now)
            {
                if (!_shown.TryGetValue(id, out var before) || before.Level != message.Level)
                {
                    messages.Add(message.Level, MessageKind.Status, message.Text);
                }
            }

            foreach (var (id, before) in _shown)
            {
                if (!now.ContainsKey(id))
                {
                    messages.Add(before.Level, MessageKind.Status, ResolvedPrefix + before.Text);
                }
            }

            _shown = now;
        }
    }
}
