using System.Globalization;
using System.Runtime.CompilerServices;
using Arcadia.App.Services;

namespace Arcadia.App.Tests;

/// <summary>
/// The system the tests run on is the same everywhere (#553): German regional format and no system keyboard layout of the
/// five, so the CI's runners (English, US layout) show what a German PC shows. A test with another system sets its own.
/// </summary>
internal static class TestSystem
{
    public static readonly CultureInfo GermanRegion = CultureInfo.GetCultureInfo("de-DE");

    [ModuleInitializer]
    internal static void UseGermanPc()
    {
        CultureInfo.DefaultThreadCurrentCulture = GermanRegion;
        Formatting.UseRegion(GermanRegion);
        SystemKeyboardLayout.Read = () => null;
    }
}
