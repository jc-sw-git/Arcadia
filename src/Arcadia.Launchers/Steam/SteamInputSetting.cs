using System.Globalization;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// How Steam Input is set for one Steam game, from the account's <c>localconfig.vdf</c>: the game's own
/// override <c>UseSteamControllerConfig</c> ("0" off, "1" or missing Steam's default, "2" on for every
/// controller; "2" seen on the owner's PC for a game set to Steam Input for all controllers, 2026-10-21),
/// and Steam's global Xbox and generic gamepad configuration support, which the default follows.
/// </summary>
public sealed record SteamInputSetting(string? GameSetting, bool XboxSupport, bool GenericSupport)
{
    /// <summary>
    /// Steam Input also takes controllers other than the Steam Controller in this game: true when it is
    /// on for every controller, or on Steam's default with Xbox or generic support on; false when it is
    /// off for the game. Null when the file doesn't tell (Steam's default without those, which a game
    /// can change itself, or a value not seen yet).
    /// </summary>
    public bool? CoversOtherControllers => GameSetting switch
    {
        "2" => true,
        "0" => false,
        null or "1" => XboxSupport || GenericSupport ? true : null,
        _ => null,
    };

    /// <summary>
    /// Steam Input takes Xbox or generic pads outside games too (Steam → Einstellungen → Controller → Erweiterte
    /// Controllereinstellungen). How Steam stores the same for PlayStation and Nintendo pads isn't known yet.
    /// </summary>
    public bool CoversOtherControllersByDefault => XboxSupport || GenericSupport;

    /// <summary>For the log.</summary>
    public string Description =>
        $"UseSteamControllerConfig {GameSetting ?? "nicht gesetzt"}, Xbox-Unterstützung {(XboxSupport ? "an" : "aus")}, generische Unterstützung {(GenericSupport ? "an" : "aus")}";

    /// <summary>The setting for <paramref name="appId"/>; null if the file is missing or broken.</summary>
    public static SteamInputSetting? Read(string? localConfigVdf, int appId)
    {
        if (localConfigVdf is null)
        {
            return null;
        }

        VdfMap? store;
        try
        {
            store = TextVdf.Parse(localConfigVdf).GetMap("UserLocalConfigStore");
        }
        catch (FormatException)
        {
            return null;
        }

        if (store is null)
        {
            return null;
        }

        var game = store.GetMap("apps")?.GetMap(appId.ToString(CultureInfo.InvariantCulture));
        return new SteamInputSetting(
            game?.GetString("UseSteamControllerConfig"),
            store.GetString("SteamController_XBoxSupport") == "1",
            store.GetString("SteamController_GenericGamepadSupport") == "1");
    }
}
