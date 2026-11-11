using Arcadia.App.Messages;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>#433: the list behind "Letzte Meldungen" and how status messages become its entries.</summary>
public sealed class RecentMessagesTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 10, 41, 0, TimeSpan.Zero));

    [Theory]
    [Trait("TestCase", "T433-8")]
    [InlineData(49, 49)]
    [InlineData(50, 50)]
    [InlineData(51, 50)]
    public void The_list_keeps_the_newest_fifty(int added, int kept)
    {
        var messages = new RecentMessages(_time, _ => { });
        for (var i = 1; i <= added; i++)
        {
            messages.Add(HealthSeverity.Info, MessageKind.Toast, $"Meldung {i}");
        }

        var entries = messages.Entries;
        Assert.Equal(kept, entries.Count);
        Assert.Equal($"Meldung {added}", entries[0].Text);
        Assert.Equal($"Meldung {added - kept + 1}", entries[^1].Text);
    }

    [Fact]
    [Trait("TestCase", "T433-13")]
    public void A_new_entry_drops_the_oldest_whatever_its_level()
    {
        var messages = new RecentMessages(_time, _ => { });
        messages.Add(HealthSeverity.Info, MessageKind.Toast, "älteste, Info");
        for (var i = 0; i < RecentMessages.Capacity - 1; i++)
        {
            messages.Add(HealthSeverity.Warning, MessageKind.Toast, "Warnung");
        }

        messages.Add(HealthSeverity.Error, MessageKind.Toast, "neu");

        Assert.DoesNotContain(messages.Entries, entry => entry.Level == HealthSeverity.Info);
        Assert.Equal("neu", messages.Entries[0].Text);
    }

    [Fact]
    [Trait("TestCase", "T433-14")]
    public void Clearing_empties_the_list_and_says_so_once()
    {
        var messages = new RecentMessages(_time, _ => { });
        var changes = 0;
        messages.Add(HealthSeverity.Error, MessageKind.Toast, "Fehler");
        messages.Changed += (_, _) => changes++;

        messages.Clear();
        messages.Clear();

        Assert.Empty(messages.Entries);
        Assert.Equal(1, changes);
    }

    [Fact]
    [Trait("TestCase", "T433-9")]
    public async Task Senders_on_other_threads_all_reach_the_list_in_their_order()
    {
        var messages = new RecentMessages(_time, _ => { });
        const int senders = 4;
        const int each = 12;
        using var start = new Barrier(senders);

        await Task.WhenAll(Enumerable.Range(0, senders).Select(sender => Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < each; i++)
            {
                messages.Add(HealthSeverity.Warning, MessageKind.Toast, $"{sender}:{i}");
            }
        })));

        var entries = messages.Entries;
        Assert.Equal(senders * each, entries.Count);
        for (var sender = 0; sender < senders; sender++)
        {
            // Newest first: each sender's own messages come in reverse order of sending.
            var own = entries.Select(e => e.Text).Where(t => t.StartsWith($"{sender}:", StringComparison.Ordinal)).Reverse();
            Assert.Equal(Enumerable.Range(0, each).Select(i => $"{sender}:{i}"), own);
        }
    }

    [Fact]
    [Trait("TestCase", "T433-17")]
    public void A_status_message_is_one_entry_that_is_marked_when_it_is_resolved()
    {
        var messages = new RecentMessages(_time, _ => { });
        var status = new StatusMessageChanges(messages, _ => { });

        status.Show([new StatusMessage("pan-missing", HealthSeverity.Warning, "Controller-Wächter (Pan) fehlt")]);
        Assert.Null(Assert.Single(messages.Entries).ResolvedAt);
        _time.Advance(TimeSpan.FromMinutes(3));
        status.Show([]);

        var entry = Assert.Single(messages.Entries);
        Assert.Equal((HealthSeverity.Warning, MessageKind.Status, ControllerSupportTexts.PanMissingTitle), (entry.Level, entry.Kind, entry.Text));
        Assert.Equal(_time.GetLocalNow(), entry.ResolvedAt);
        Assert.StartsWith("Behoben ", InfoMessage.Of(entry).EndText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("TestCase", "T433-18")]
    public void A_changed_level_adds_one_entry_and_an_unchanged_refresh_none()
    {
        var messages = new RecentMessages(_time, _ => { });
        var status = new StatusMessageChanges(messages, _ => { });
        var pan = new StatusMessage("pan-not-running", HealthSeverity.Warning, "Pan läuft nicht");

        status.Show([pan]);
        for (var i = 0; i < 30; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            status.Show([pan]);
        }

        status.Show([pan with { Level = HealthSeverity.Error }]);

        Assert.Equal([HealthSeverity.Error, HealthSeverity.Warning], messages.Entries.Select(e => e.Level));
        Assert.Equal([null, HealthSeverity.Error], messages.Entries.Select(e => e.ChangedTo));
        Assert.Equal("Jetzt Fehler", InfoMessage.Of(messages.Entries[1]).EndText);
    }

    [Fact]
    [Trait("TestCase", "T433-17")]
    public void A_resolved_error_keeps_its_level()
    {
        var messages = new RecentMessages(_time, _ => { });
        var status = new StatusMessageChanges(messages, _ => { });

        status.Show([new StatusMessage("pan-not-running", HealthSeverity.Error, "Pan läuft nicht")]);
        status.Show([]);

        Assert.All(messages.Entries, entry => Assert.Equal(HealthSeverity.Error, entry.Level));
    }
}
