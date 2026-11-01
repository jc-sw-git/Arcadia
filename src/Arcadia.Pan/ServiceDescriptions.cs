using System.Globalization;

namespace Arcadia.Pan;

/// <summary>The service's description in the system's UI language (owner, 2026-10-26); any other language gets English.</summary>
public static class ServiceDescriptions
{
    public static string For(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "de" => "Wächter-Prozess für Arcadia: sorgt für eine saubere Controller-Emulation und gibt die echten Controller allen Programmen zurück, wenn Arcadia unerwartet endet.",
        "fr" => "Processus gardien d'Arcadia : garantit une émulation de manette propre et rend les vraies manettes à tous les programmes si Arcadia s'arrête de façon inattendue.",
        "es" => "Proceso guardián de Arcadia: garantiza una emulación de mandos limpia y devuelve los mandos reales a todos los programas si Arcadia se cierra de forma inesperada.",
        "it" => "Processo guardiano di Arcadia: garantisce un'emulazione pulita dei controller e restituisce i controller reali a tutti i programmi se Arcadia si chiude in modo imprevisto.",
        _ => "Guardian process for Arcadia: keeps controller emulation clean and gives the real controllers back to every program if Arcadia ends unexpectedly.",
    };

    /// <summary>The system's UI language, not the user's: the service belongs to the machine.</summary>
    public static string ForSystem() => For(CultureInfo.InstalledUICulture);
}
