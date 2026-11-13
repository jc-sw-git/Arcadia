using System.Globalization;
using System.Runtime.CompilerServices;

namespace Arcadia.App.Tests;

/// <summary>
/// The tests read the German texts, whatever language the machine they run on speaks (a CI runner is English): a test
/// that wants another language sets <see cref="CultureInfo.CurrentUICulture"/> itself.
/// </summary>
internal static class GermanUiLanguage
{
    [ModuleInitializer]
    internal static void Set() => CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
}
