using Arcadia.ControllerSupport;

namespace Arcadia.App.Messages;

/// <summary>A status message as the header shows it, by a key that stays the same while it is shown.</summary>
public sealed record StatusMessage(string Id, HealthSeverity Level, string Text);

/// <summary>
/// Turns the status messages shown now into entries of <see cref="RecentMessages"/> (#433): one when a message appears
/// and one when its level changes (the older entry then says so); when it goes, its entry is marked "Behoben" in place
/// (owner), so every status entry without the mark is still pending. A refresh that changes nothing changes nothing.
/// </summary>
public sealed class StatusMessageChanges(RecentMessages messages, Action<string> log)
{
    private readonly object _gate = new();
    private Dictionary<string, (StatusMessage Message, long Entry)> _shown = [];

    public void Show(IEnumerable<StatusMessage> current)
    {
        // Under the gate, so two threads reporting at once change the list in the order they were seen.
        lock (_gate)
        {
            var now = new Dictionary<string, (StatusMessage Message, long Entry)>();
            foreach (var message in current)
            {
                if (now.ContainsKey(message.Id))
                {
                    continue;
                }

                if (!_shown.TryGetValue(message.Id, out var before))
                {
                    now[message.Id] = (message, messages.Add(message.Level, MessageKind.Status, message.Text));
                }
                else if (before.Message.Level != message.Level)
                {
                    messages.End(before.Entry, changedTo: message.Level);
                    now[message.Id] = (message, messages.Add(message.Level, MessageKind.Status, message.Text));
                }
                else
                {
                    now[message.Id] = before;
                }
            }

            foreach (var (id, before) in _shown)
            {
                if (!now.ContainsKey(id))
                {
                    log($"Status behoben ({before.Message.Level}): {before.Message.Text}");
                    messages.End(before.Entry);
                }
            }

            _shown = now;
        }
    }
}
