using Arcadia.App.Services;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>One line of "So funktioniert es": a button and what it does in the mouse mode.</summary>
public sealed record MouseModeHelpRow(string Input, string Effect);

/// <summary>
/// The rows of the mouse mode (#64) in the category "Controller": how it is switched on and everything that depends on
/// it, in one group. Every change is saved at once and applies at once.
/// </summary>
public sealed class MouseModeRows
{
    private const string Group = "MAUSMODUS";

    /// <summary>The values of "Mausmodus", in the order of <see cref="MouseModeActivation"/>.</summary>
    private static readonly string[] ActivationNames = ["Aus", "Von Hand", "Automatisch"];

    /// <summary>The values of "Beenden des Mausmodus in Spielen erzwingen", in the order of <see cref="MouseModeInGames"/>.</summary>
    private static readonly string[] InGamesNames = ["Nein", "Ja", "Individuell"];

    private readonly SettingsChanges _changes;
    private readonly ChoiceRowViewModel _activation;
    private readonly ChoiceRowViewModel _hold;
    private readonly ChoiceRowViewModel _speed;
    private readonly SwitchRowViewModel _endInFront;
    private readonly ChoiceRowViewModel _inGames;
    private readonly ChoiceRowViewModel _perGameDefault;

    public MouseModeRows(SettingsChanges changes)
    {
        _changes = changes;
        _activation = new ChoiceRowViewModel(
            "mouse-mode",
            "Mausmodus",
            () => (int)Current.Activation,
            value => Change(s => s with { Activation = (MouseModeActivation)value }),
            value => ActivationNames[value],
            (int)MouseModeActivation.Off,
            (int)MouseModeActivation.Automatic)
        {
            Group = Group,
            Subtitle = "Der Controller steuert den Mauszeiger, auch außerhalb von Arcadia",
        };
        _hold = new ChoiceRowViewModel(
            "mouse-mode-hold",
            "Haltedauer",
            () => Current.HoldSeconds,
            value => Change(s => s with { HoldSeconds = value }),
            ChoiceRowViewModel.Seconds,
            MouseModeSettings.Min,
            MouseModeSettings.Max)
        {
            Group = Group,
            Indent = 1,
            Subtitle = "So lange müssen ⧉ und ≡ gehalten werden.",
            Info = [new InfoText("So lange müssen ⧉ und ≡ gehalten werden.")],
        };
        _speed = new ChoiceRowViewModel(
            "mouse-mode-speed",
            "Zeigergeschwindigkeit",
            () => Current.PointerSpeed,
            value => Change(s => s with { PointerSpeed = value }),
            value => value.ToString(Formatting.Culture),
            MouseModeSettings.Min,
            MouseModeSettings.Max)
        {
            Group = Group,
            Indent = 1,
            Subtitle = "Wie schnell [LS] den Mauszeiger bewegt.",
            Info = [new InfoText("Wie schnell [LS] den Mauszeiger bewegt. [L3] macht ihn langsamer.")],
        };
        _endInFront = new SwitchRowViewModel(
            "mouse-mode-end-in-front",
            "Mausmodus beenden, wenn Arcadia im Vordergrund ist",
            () => Current.EndWhenArcadiaInFront,
            value => Change(s => s with { EndWhenArcadiaInFront = value }))
        {
            Group = Group,
            Indent = 1,
            Subtitle = "Nur aktiv im Modus „Von Hand“",
            Info = [new InfoText("Sobald du wieder in das Fenster von Arcadia klickst oder Arcadia nach vorne holst (z. B. durch „Mit ⧉ zurück zu Arcadia“), schaltet sich der Mausmodus automatisch ab. Aktivierst du den Mausmodus von Hand, während Arcadia bereits im Vordergrund ist, bleibt er so lange aktiv, bis Arcadia das nächste Mal in den Vordergrund zurückgeholt wird, also z. B. durch Raus- und wieder Reinklicken.")],
        };
        _inGames = new ChoiceRowViewModel(
            "mouse-mode-in-games",
            "Beenden des Mausmodus in Spielen erzwingen",
            () => (int)Current.InGames,
            value => Change(s => s with { InGames = (MouseModeInGames)value }),
            value => InGamesNames[value],
            (int)MouseModeInGames.Allowed,
            (int)MouseModeInGames.PerGame)
        {
            Group = Group,
            Indent = 1,
            Subtitle = "Nur aktiv im Modus „Von Hand“",
        };
        _perGameDefault = new ChoiceRowViewModel(
            "mouse-mode-per-game-default",
            "Standardeinstellung bei „Individuell“",
            () => Current.PerGameDefaultAllowed ? 0 : 1,
            value => Change(s => s with { PerGameDefaultAllowed = value == 0 }),
            value => value == 0 ? GameMouseModes.Allowed : GameMouseModes.Blocked,
            0,
            1)
        {
            Group = Group,
            Indent = 2,
            Subtitle = "Vorausgewählt für jedes Spiel auf der Detailseite",
            Info = [new InfoText("„Standard“ ist für jedes Spiel auf der Detailseite vorausgewählt. „Erlaubt“ wird den Mausmodus erlauben. „Deaktiviert“ wird ihn deaktivieren.")],
        };
        Update();
    }

    /// <summary>The mouse mode's buttons, as <see cref="Arcadia.Input.MouseMode"/> maps them.</summary>
    public static IReadOnlyList<MouseModeHelpRow> HelpRows { get; } =
    [
        new("LS", "Mauszeiger"),
        new("L3", "Antippen: langsamer Zeiger an/aus · Halten: langsam, solange gehalten"),
        new("RT oder A", "Linke Maustaste (gedrückt halten = ziehen)"),
        new("LT oder X", "Rechte Maustaste"),
        new("RS", "Mausrad, auch seitwärts"),
        new("D-Pad", "Pfeiltasten"),
        new("B", "Esc"),
        new("LB / RB", "Umschalt+Tab / Tab"),
        new("≡", "Enter"),
        new("Y", "Bildschirmtastatur (B blendet sie aus)"),
        new("⧉ halten", "Holt Arcadia nach vorne (Einstellung „Zurück zu Arcadia“)"),
        new("⧉ + ≡ halten", "Zurück zum normalen Controller (bei „Automatisch“, bis Arcadia wieder vorne war)"),
    ];

    /// <summary>Where the mouse mode can't help, so nobody wonders why.</summary>
    public static IReadOnlyList<string> HelpLimits { get; } =
    [
        "Windows-Abfragen nach Administratorrechten (UAC) laufen auf einem geschützten Bildschirm, den keine Eingabe von Arcadia erreicht.",
        "Fenster von Programmen, die als Administrator laufen, lassen Eingaben eines Arcadia ohne Administratorrechte nicht durch.",
        "Über Spielen im exklusiven Vollbild zeigt Windows den Rand, den Hinweis und die Bildschirmtastatur nicht an; das Umschalten spürst du dann nur am Vibrieren.",
    ];

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsRowViewModel> Rows => [_activation, _hold, _speed, _endInFront, _inGames, _perGameDefault];

    private MouseModeSettings Current => _changes.Current.MouseMode;

    /// <summary>Below the description of "Mausmodus", in a box of its own (owner, 2026-10-22).</summary>
    private static readonly InfoSection Help = new("SO FUNKTIONIERT ES",
    [
        new InfoText("⧉ + ≡ gedrückt halten, bis der Bildschirmrand voll ist: Der Controller steuert jetzt die Maus, auch in Fenstern außerhalb von Arcadia. Genauso geht es zurück. Jeder verbundene Controller kann umschalten und den Zeiger bewegen."),
        .. HelpRows.Select(r => new InfoKey(r.Input, r.Effect)),
        new InfoHeading("GRENZEN"),
        .. HelpLimits.Select(l => new InfoBullet(l)),
    ]);

    /// <summary>What the chosen way of switching does, under "Mausmodus".</summary>
    private string ActivationText => Current.Activation switch
    {
        MouseModeActivation.Off => "Der Controller bleibt immer ein Controller.",
        MouseModeActivation.Automatic => "Bei „Automatisch“ schaltet Arcadia automatisch in den Mausmodus, sobald Arcadia (oder ein Arcadia bekanntes Spiel) nicht mehr den Fokus hat, das heißt nicht mehr im Vordergrund läuft. Der Controller kann dann den Mauszeiger bedienen. Unten unter „So funktioniert es“ erfährst du mehr dazu. Kommt Arcadia (oder ein Spiel) zurück in den Fokus, wird der Mausmodus automatisch beendet. Dies passiert auch dann, wenn „Mausmodus beenden, wenn Arcadia im Vordergrund ist“ ausgeschaltet ist.",
        _ => "Durch Halten von ⧉ + ≡ wechselst du in den Mausmodus. Dies erlaubt dem Controller, den Mauszeiger zu steuern. Erneutes Halten schaltet den Modus aus. Unten unter „So funktioniert es“ erfährst du mehr dazu.",
    };

    /// <summary>What the chosen value of "Beenden des Mausmodus in Spielen erzwingen" does (owner's wording, 2026-10-21).</summary>
    private string InGamesText => Current.InGames switch
    {
        MouseModeInGames.Blocked => "Der Mausmodus wird automatisch beendet und kann auch nicht mehr von Hand aktiviert werden, sobald ein Spiel im Vordergrund läuft. Das Spiel muss dafür in Arcadias Bibliothek vorhanden sein.",
        MouseModeInGames.PerGame => "Es kann pro Spiel (auf der Detailseite) entschieden werden, ob der Mausmodus beendet wird oder erlaubt ist.",
        _ => "Der Mausmodus bleibt auch in Spielen aktiv und kann auch aktiviert werden, wenn das Spiel gerade im Vordergrund läuft.",
    };

    private void Change(Func<MouseModeSettings, MouseModeSettings> change)
    {
        _changes.Apply(s => s with { MouseMode = change(s.MouseMode) }, "Mausmodus");
        Update();
    }

    /// <summary>"Aus" leaves every other row greyed out, with its value kept; some only count for "Von Hand".</summary>
    private void Update()
    {
        var enabled = Current.Activation != MouseModeActivation.Off;
        var manual = Current.Activation == MouseModeActivation.Manual;
        _hold.IsDimmed = !enabled;
        _speed.IsDimmed = !enabled;
        _endInFront.IsDimmed = !manual;
        _inGames.IsDimmed = !manual;
        _perGameDefault.IsDimmed = !manual || Current.InGames != MouseModeInGames.PerGame;
        _activation.Info = [new InfoText(ActivationText), Help];
        _inGames.Info = [new InfoText(InGamesText)];
    }
}

/// <summary>"Zurück zu Arcadia" (#64): View held brings Arcadia back, with or without the mouse mode; its own group.</summary>
public sealed class ReturnWithViewRows
{
    private const string Group = "ZURÜCK ZU ARCADIA";

    private readonly SettingsChanges _changes;
    private readonly SwitchRowViewModel _enabled;
    private readonly ChoiceRowViewModel _hold;

    public ReturnWithViewRows(SettingsChanges changes)
    {
        _changes = changes;
        _enabled = new SwitchRowViewModel("return-with-view", "Mit ⧉ zurück zu Arcadia", () => Current.Enabled, value => Change(s => s with { Enabled = value }))
        {
            Group = Group,
            Subtitle = "⧉ gedrückt halten holt Arcadia nach vorne",
            Info = [new InfoText("⧉ gedrückt halten holt Arcadia nach vorne, wenn es im Hintergrund ist – aus einem Spiel, einem Launcher oder dem Mausmodus. In Arcadia öffnet ⧉ weiter das Menü.")],
        };
        _hold = new ChoiceRowViewModel(
            "return-with-view-hold",
            "Haltedauer",
            () => Current.HoldSeconds,
            value => Change(s => s with { HoldSeconds = value }),
            ChoiceRowViewModel.Seconds,
            ReturnWithViewSettings.Min,
            ReturnWithViewSettings.Max)
        {
            Group = Group,
            Indent = 1,
            Subtitle = "So lange muss ⧉ gehalten werden.",
            Info = [new InfoText("So lange muss ⧉ gehalten werden. Kurz gedrückt schiebt ⧉ die Bildschirmtastatur des Mausmodus an den anderen Bildschirmrand.")],
        };
        Update();
    }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsRowViewModel> Rows => [_enabled, _hold];

    private ReturnWithViewSettings Current => _changes.Current.ReturnWithView;

    private void Change(Func<ReturnWithViewSettings, ReturnWithViewSettings> change)
    {
        _changes.Apply(s => s with { ReturnWithView = change(s.ReturnWithView) }, "Zurück zu Arcadia");
        Update();
    }

    private void Update() => _hold.IsDimmed = !Current.Enabled;
}

/// <summary>The values of "Individueller Mausmodus" on a game (owner, 2026-10-20).</summary>
public static class GameMouseModes
{
    public const string Allowed = "Erlaubt";
    public const string Blocked = "Deaktiviert";

    /// <summary>"Standard" names what it means now, e.g. "Standard (erlaubt)" (owner, 2026-10-21).</summary>
    public static string Of(bool? choice, bool defaultAllowed) => choice switch
    {
        true => Allowed,
        false => Blocked,
        null => defaultAllowed ? "Standard (erlaubt)" : "Standard (deaktiviert)",
    };

    /// <summary>Standard → Erlaubt → Deaktiviert → Standard.</summary>
    public static bool? Next(bool? choice) => choice switch
    {
        null => true,
        true => false,
        false => null,
    };
}
