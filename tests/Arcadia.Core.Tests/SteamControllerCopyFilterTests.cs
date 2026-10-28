using System.Buffers.Binary;
using Arcadia.ControllerSupport.Copy;
using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Tests.Pads;

/// <summary>
/// The Steam Controller copy's rules (#242): no firmware, bootloader, reset, pairing or unknown message from Steam ever
/// reaches the real puck (decision 8, the owner: "no bricked Steam Controllers under any circumstances"), and Steam reads
/// rest from the copy except the Steam button and "…" unless the copy passes input.
/// </summary>
public sealed class SteamControllerCopyFilterTests
{
    private static readonly ComboWithholdingOptions CombosOn = new(BackAndStart: true);

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly List<string> _firmware = [];
    private readonly SteamControllerCopyFilter _filter;

    public SteamControllerCopyFilterTests()
    {
        _filter = new SteamControllerCopyFilter(_time, _log.Add);
        _filter.UpdateTraffic += _firmware.Add;
    }

    /// <summary>The allow list, written out on its own so a change to it shows here as a deliberate second edit.</summary>
    private static readonly byte[] AllowedFeatureTypes = [0x81, 0x82, 0x83, 0x85, 0x87, 0x8E, 0xAE, 0xB4, 0xC1];
    private static readonly byte[] HapticFeatureTypes = [0x8F, 0xEA, 0xEB];
    private static readonly byte[] FirmwareFeatureTypes = [0xB7, 0xB8, 0xB9];
    private static readonly byte[] HapticOutputIds = [0x80, 0x81, 0x82, 0x83, 0x84, 0x85];

    public static TheoryData<byte> AllTypes()
    {
        var data = new TheoryData<byte>();
        for (var type = 0; type < 256; type++)
        {
            data.Add((byte)type);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    [Trait("TestCase", "T242-1")]
    public void Every_feature_message_type_is_classified_by_the_allow_list_and_nothing_else_reaches_the_puck(byte type)
    {
        var expected = AllowedFeatureTypes.Contains(type) ? SteamMessageKind.Allowed
            : HapticFeatureTypes.Contains(type) ? SteamMessageKind.Haptics
            : FirmwareFeatureTypes.Contains(type) ? SteamMessageKind.Firmware
            : SteamMessageKind.Blocked;

        Assert.Equal(expected, SteamControllerMessages.OfFeature(type));
        foreach (var reportId in new byte[] { 1, 2 })
        {
            byte[] message = [reportId, type, 0x02, 0xAA, 0xBB];
            Assert.Equal(expected == SteamMessageKind.Allowed, _filter.FromSteam(SteamReportKind.SetFeature, message, forwarding: false));
            Assert.Equal(expected is SteamMessageKind.Allowed or SteamMessageKind.Haptics, _filter.FromSteam(SteamReportKind.SetFeature, message, forwarding: true));
        }
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    [Trait("TestCase", "T242-1")]
    public void Every_output_report_id_but_the_haptics_is_dropped_and_haptics_pass_only_while_the_copy_passes_input(byte reportId)
    {
        var haptics = HapticOutputIds.Contains(reportId);
        byte[] report = [reportId, 0x01, 0x10, 0x00];

        Assert.Equal(haptics ? SteamMessageKind.Haptics : SteamMessageKind.Blocked, SteamControllerMessages.OfOutput(reportId));
        Assert.False(_filter.FromSteam(SteamReportKind.Output, report, forwarding: false));
        Assert.Equal(haptics, _filter.FromSteam(SteamReportKind.Output, report, forwarding: true));
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    [Trait("TestCase", "T242-1")]
    public void A_feature_report_other_than_1_or_2_never_passes_whatever_it_carries(byte reportId)
    {
        if (reportId is 1 or 2)
        {
            return;
        }

        Assert.False(_filter.FromSteam(SteamReportKind.SetFeature, [reportId, 0x83, 0x00], forwarding: true));
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public void The_types_spike_239_saw_dropped_stay_dropped()
    {
        // Firmware and bootloader messages, factory reset, serial, pairing records, and Steam's four unknown ones.
        foreach (var type in new byte[] { 0x86, 0xA9, 0xAD, 0xAF, 0xB0, 0xB7, 0xB8, 0xB9, 0xDC, 0xE2, 0xED, 0xF3 })
        {
            Assert.False(_filter.FromSteam(SteamReportKind.SetFeature, [1, type, 0], forwarding: true), $"0x{type:X2}");
        }
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public void Empty_short_or_other_reports_never_pass_but_reads_do()
    {
        Assert.False(_filter.FromSteam(SteamReportKind.SetFeature, [], forwarding: true));
        Assert.False(_filter.FromSteam(SteamReportKind.SetFeature, [1], forwarding: true));
        Assert.False(_filter.FromSteam(SteamReportKind.Output, [], forwarding: true));
        Assert.False(_filter.FromSteam(SteamReportKind.Other, [1, 0x83], forwarding: true));
        Assert.True(_filter.FromSteam(SteamReportKind.GetReport, [1], forwarding: false));
    }

    [Fact]
    [Trait("TestCase", "T242-1")]
    public void A_handler_that_throws_still_drops_the_firmware_message()
    {
        _filter.UpdateTraffic += _ => throw new InvalidOperationException("Dialog kaputt");

        Assert.False(_filter.FromSteam(SteamReportKind.SetFeature, [1, 0xB7, 0x00], forwarding: true));
    }

    [Fact]
    [Trait("TestCase", "T242-8")]
    public void Every_dropped_message_but_the_routine_ones_counts_as_update_traffic_and_each_type_is_logged_once_without_its_payload()
    {
        for (var i = 0; i < 3; i++)
        {
            _filter.FromSteam(SteamReportKind.SetFeature, [1, 0xB8, 0x10, 0x42, 0x42], forwarding: true);
            _filter.FromSteam(SteamReportKind.SetFeature, [1, 0xDC, 0x00], forwarding: true);
        }

        _filter.FromSteam(SteamReportKind.SetFeature, [1, 0x86], forwarding: true);

        Assert.Equal(
        [
            "Steam-Controller-Kopie: Firmware-Nachricht AUDIO_UPDATE_DATA (0xB8) verworfen (5 Bytes)",
            "Steam-Controller-Kopie: Nachricht unbekannt (0xDC) verworfen (3 Bytes)",
            "Steam-Controller-Kopie: Nachricht FACTORY_RESET (0x86) verworfen (2 Bytes)",
        ], _log);
        Assert.DoesNotContain(_log, line => line.Contains("42 42") || line.Contains("0x42"));

        _filter.FromSteam(SteamReportKind.SetFeature, [1, 0xA3, 0x00], forwarding: true);
        _filter.FromSteam(SteamReportKind.Output, [0x81, 0x00], forwarding: false);

        // 0xDC and 0xA3 Steam sends all along, haptics while resting are dropped as routine: none of them counts.
        Assert.Equal(
        [
            "Nachricht AUDIO_UPDATE_DATA (0xB8)", "Nachricht AUDIO_UPDATE_DATA (0xB8)", "Nachricht AUDIO_UPDATE_DATA (0xB8)",
            "Nachricht FACTORY_RESET (0x86)",
        ], _firmware);
    }

    [Fact]
    [Trait("TestCase", "T242-2")]
    public void While_resting_Steam_reads_a_controller_at_rest_with_only_the_Steam_button_and_the_quick_access_button()
    {
        var state = State(SteamControllerCopyFilter.StateReport, buttons: 0x0001_0011 | 0x200, axes: 0x7F);
        var output = new byte[state.Length];

        var length = _filter.ToSteam(state, output, forwarding: false, CombosOn);

        Assert.Equal(state.Length, length);
        Assert.Equal(SteamControllerCopyFilter.SteamButton | SteamControllerCopyFilter.QamButton, Buttons(output));
        Assert.All(output[6..30], b => Assert.Equal(0, b));
        Assert.All(output[40..46], b => Assert.Equal(0, b));
        // Sequence, IMU timestamp, accelerometer and orientation stay as the puck sent them.
        Assert.Equal(state[1], output[1]);
        Assert.Equal(state[30..40], output[30..40]);
        Assert.Equal(state[46..], output[46..]);
    }

    [Fact]
    [Trait("TestCase", "T242-2")]
    public void The_timestamp_state_report_rests_its_trackpads_behind_the_trackpad_timestamp()
    {
        var state = State(SteamControllerCopyFilter.StateReportWithTimestamp, buttons: 0x0020_0000, axes: 0x33, length: 46);
        var output = new byte[state.Length];

        _filter.ToSteam(state, output, forwarding: false, CombosOn);

        Assert.Equal(0u, Buttons(output));
        Assert.All(output[6..18], b => Assert.Equal(0, b));
        Assert.Equal(state[18..20], output[18..20]);
        Assert.All(output[20..32], b => Assert.Equal(0, b));
        Assert.All(output[40..46], b => Assert.Equal(0, b));
    }

    [Fact]
    [Trait("TestCase", "T242-3")]
    public void While_passing_input_Steam_reads_everything_the_puck_sent()
    {
        var state = State(SteamControllerCopyFilter.StateReport, buttons: 0x0080_0201, axes: 0x5A);
        var output = new byte[state.Length];

        _filter.ToSteam(state, output, forwarding: true, CombosOn);

        Assert.Equal(state, output);
    }

    [Fact]
    [Trait("TestCase", "T242-3")]
    public void Arcadias_combo_never_reaches_Steam_while_passing_input()
    {
        var combo = SteamControllerCopyFilter.ViewButton | SteamControllerCopyFilter.MenuButton | 0x1;
        var output = new byte[54];

        _filter.ToSteam(State(SteamControllerCopyFilter.StateReport, buttons: SteamControllerCopyFilter.ViewButton), output, forwarding: true, CombosOn);
        Assert.Equal(0u, Buttons(output));
        _time.Advance(TimeSpan.FromMilliseconds(30));
        _filter.ToSteam(State(SteamControllerCopyFilter.StateReport, buttons: combo), output, forwarding: true, CombosOn);
        Assert.Equal(1u, Buttons(output));
        _time.Advance(TimeSpan.FromSeconds(1));
        _filter.ToSteam(State(SteamControllerCopyFilter.StateReport, buttons: combo), output, forwarding: true, CombosOn);
        Assert.Equal(1u, Buttons(output));
    }

    [Fact]
    [Trait("TestCase", "T242-3")]
    public void With_the_mouse_mode_off_View_and_Menu_pass_as_they_are()
    {
        var output = new byte[54];
        var both = SteamControllerCopyFilter.ViewButton | SteamControllerCopyFilter.MenuButton;

        _filter.ToSteam(State(SteamControllerCopyFilter.StateReport, buttons: both), output, forwarding: true, new ComboWithholdingOptions(BackAndStart: false));

        Assert.Equal(both, Buttons(output));
    }

    [Fact]
    [Trait("TestCase", "T242-7")]
    public void Battery_and_connection_state_always_pass_other_reports_only_while_passing_input()
    {
        var output = new byte[16];
        foreach (var id in new byte[] { 0x43, 0x46, 0x79 })
        {
            Assert.Equal(4, _filter.ToSteam([id, 1, 2, 3], output, forwarding: false, CombosOn));
            Assert.Equal([id, 1, 2, 3], output[..4]);
        }

        foreach (var id in new byte[] { 0x40, 0x41, 0x44, 0x7B, 0x99 })
        {
            Assert.Equal(0, _filter.ToSteam([id, 1, 2, 3], output, forwarding: false, CombosOn));
            Assert.Equal(4, _filter.ToSteam([id, 1, 2, 3], output, forwarding: true, CombosOn));
        }
    }

    [Fact]
    public void A_state_report_too_short_for_its_buttons_or_an_output_too_small_sends_nothing()
    {
        Assert.Equal(0, _filter.ToSteam([0x42, 1, 2], new byte[8], forwarding: true, CombosOn));
        Assert.Equal(0, _filter.ToSteam([0x43, 1, 2], new byte[2], forwarding: true, CombosOn));
        Assert.Equal(0, _filter.ToSteam([], new byte[2], forwarding: true, CombosOn));
    }

    private static byte[] State(byte id, uint buttons, byte axes = 0, int length = 54)
    {
        var state = new byte[length];
        state[0] = id;
        state[1] = 0x17;
        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(2), buttons);
        for (var i = 6; i < length; i++)
        {
            state[i] = (byte)(axes + i);
        }

        return state;
    }

    private static uint Buttons(byte[] state) => BinaryPrimitives.ReadUInt32LittleEndian(state.AsSpan(2));
}
