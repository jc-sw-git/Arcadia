namespace Arcadia.ControllerSupport.Copy;

/// <summary>What the copy does with a message Steam sends to the Steam Controller (#242, decision 8).</summary>
public enum SteamMessageKind
{
    /// <summary>On the allow list: settings, mapping queries, battery and connection state. Always passes to the real puck.</summary>
    Allowed,

    /// <summary>Haptics and rumble: pass only while the copy passes input (decision 7).</summary>
    Haptics,

    /// <summary>A known firmware or flash update message: dropped, and Arcadia tells the player (<see cref="FirmwareAttemptWatch"/>).</summary>
    Firmware,

    /// <summary>Everything else: factory reset, serial number, pairing records and every unknown type. Dropped.</summary>
    Blocked,
}

/// <summary>
/// The allow list of the Steam Controller copy (#242): which of Steam's messages reach the real puck. Platform-neutral,
/// so a Linux copy uses the same list. Names from SDL's <c>controller_constants.h</c> and <c>controller_structs.h</c>.
/// </summary>
/// <remarks>
/// The owner's rule: no bricked Steam Controller under any circumstances. So the list holds only what Steam sent in spike
/// #239 (<c>docs/controller-support/emulated-steam-controller.md</c>) and the haptics; every other type is dropped,
/// whatever it is called. Changing it needs a check on the owner's PC and a line in <c>docs/controller-support/</c>.
/// </remarks>
public static class SteamControllerMessages
{
    /// <summary>
    /// Feature messages (SET_REPORT feature, report 1 or 2, the type in the next byte) Steam sent in #239 and needs:
    /// without them Steam can't read the controller's state, layout or battery, or set its settings.
    /// </summary>
    private static readonly Dictionary<byte, string> AllowedFeatures = new()
    {
        [0x81] = "CLEAR_DIGITAL_MAPPINGS",
        [0x82] = "GET_DIGITAL_MAPPINGS",
        [0x83] = "GET_ATTRIBUTES_VALUES",
        [0x85] = "SET_DEFAULT_DIGITAL_MAPPINGS",
        [0x87] = "SET_SETTINGS_VALUES",
        [0x8E] = "LOAD_DEFAULT_SETTINGS",

        // Steam + Y on the controller (#295): without it the controller doesn't switch off while the copy runs.
        [0x9F] = "TURN_OFF_CONTROLLER",
        [0xAE] = "GET_STRING_ATTRIBUTE",
        [0xB4] = "DONGLE_GET_WIRELESS_STATE",
        [0xC1] = "SET_AUDIO_MAPPING",
    };

    /// <summary>Feature messages that only make the controller vibrate.</summary>
    private static readonly Dictionary<byte, string> HapticFeatures = new()
    {
        [0x8F] = "TRIGGER_HAPTIC_PULSE",
        [0xEA] = "TRIGGER_HAPTIC_CMD",
        [0xEB] = "TRIGGER_RUMBLE_CMD",
    };

    /// <summary>
    /// Feature messages that start or write a firmware update: <c>0x90</c> Steam sends to start one (spike #295: the real
    /// device restarts into its bootloader <c>28DE:1005</c> or <c>1007</c>, and Steam's hardwareupdater.exe flashes it
    /// there), and the three audio update messages SDL names. Not an SDL name: <c>0x90</c> is named after what it does.
    /// </summary>
    private static readonly Dictionary<byte, string> FirmwareFeatures = new()
    {
        [0x90] = "REBOOT_INTO_BOOTLOADER",
        [0xB7] = "AUDIO_UPDATE_START",
        [0xB8] = "AUDIO_UPDATE_DATA",
        [0xB9] = "AUDIO_UPDATE_COMPLETE",
    };

    /// <summary>Known types that are dropped on purpose (named in the log rather than as unknown).</summary>
    private static readonly Dictionary<byte, string> BlockedFeatures = new()
    {
        [0x86] = "FACTORY_RESET",
        [0xA9] = "SET_SERIAL_NUMBER",
        [0xAD] = "ENABLE_PAIRING",
        [0xAF] = "RADIO_ERASE_RECORDS",
        [0xB0] = "RADIO_WRITE_RECORD",
        [0xB1] = "SET_DONGLE_SETTING",
        [0xB2] = "DONGLE_DISCONNECT_DEVICE",
        [0xB3] = "DONGLE_COMMIT_DEVICE",
    };

    /// <summary>Output reports of the puck's vendor collection whose report id says they are haptics (SDL: <c>ValveTritonOutReportMessageIDs</c>).</summary>
    private static readonly Dictionary<byte, string> HapticOutputs = new()
    {
        [0x80] = "HAPTIC_RUMBLE",
        [0x81] = "HAPTIC_PULSE",
        [0x82] = "HAPTIC_COMMAND",
        [0x83] = "HAPTIC_LFO_TONE",
        [0x84] = "HAPTIC_LOG_SWEEP",
        [0x85] = "HAPTIC_SCRIPT",
    };

    /// <summary>What happens with a feature message of this type.</summary>
    public static SteamMessageKind OfFeature(byte type) =>
        AllowedFeatures.ContainsKey(type) ? SteamMessageKind.Allowed
        : HapticFeatures.ContainsKey(type) ? SteamMessageKind.Haptics
        : FirmwareFeatures.ContainsKey(type) ? SteamMessageKind.Firmware
        : SteamMessageKind.Blocked;

    /// <summary>What happens with an output report of this id: haptics, or dropped.</summary>
    public static SteamMessageKind OfOutput(byte reportId) =>
        HapticOutputs.ContainsKey(reportId) ? SteamMessageKind.Haptics : SteamMessageKind.Blocked;

    /// <summary>For the log: the name SDL gives the type, or its hex value.</summary>
    public static string NameOfFeature(byte type) =>
        AllowedFeatures.TryGetValue(type, out var name) || HapticFeatures.TryGetValue(type, out name)
        || FirmwareFeatures.TryGetValue(type, out name) || BlockedFeatures.TryGetValue(type, out name)
            ? $"{name} (0x{type:X2})"
            : $"unbekannt (0x{type:X2})";

    public static string NameOfOutput(byte reportId) =>
        HapticOutputs.TryGetValue(reportId, out var name) ? $"{name} (0x{reportId:X2})" : $"unbekannt (0x{reportId:X2})";
}
