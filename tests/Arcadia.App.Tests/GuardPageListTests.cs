using System.Reflection;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Tests.Guards;
using Arcadia.App.ViewModels;

namespace Arcadia.App.Tests;

/// <summary>
/// The window guards' page list (<see cref="GuardPages"/>, #518) names every page, dialog and settings category, so a new
/// one can't be left out of the guards. Runs without a window, in every PR.
/// </summary>
public sealed class GuardPageListTests
{
    /// <summary>Pass the input on to what they show and show nothing of their own.</summary>
    private static readonly Type[] Routers = [typeof(MainViewModel), typeof(PanDialogsViewModel)];

    [Fact]
    public void Every_page_dialog_and_settings_category_is_in_the_guards_page_list()
    {
        var listed = GuardPages.All.SelectMany(page => page.Shows).ToHashSet();

        var missing = ShownViewModels().Where(type => !listed.Contains(type)).Select(type => type.Name).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, $"Nicht in tests/Arcadia.App.Tests/Guards/GuardPages.cs: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_entry_has_its_own_name_and_says_why_the_guards_skip_it()
    {
        Assert.Equal(GuardPages.All.Count, GuardPages.All.Select(page => page.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(GuardPages.All.Where(page => !page.IsWalked), page => Assert.False(string.IsNullOrWhiteSpace(page.NotWalked), page.Name));
    }

    /// <summary>
    /// The view models a player sees as a page, dialog or settings category: the pages (<see cref="IPage"/>), the
    /// categories, what the main view model opens by setting one of its properties, and anything else that takes the
    /// controller's input itself (a public <c>Handle(UiAction)</c>).
    /// </summary>
    private static IEnumerable<Type> ShownViewModels()
    {
        var viewModels = typeof(ViewModelBase).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsSubclassOf(typeof(ViewModelBase)) && !Routers.Contains(type));
        var openedByMain = typeof(MainViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: false })
            .Select(property => property.PropertyType)
            .ToHashSet();

        return viewModels.Where(type => typeof(IPage).IsAssignableFrom(type)
            || type.IsSubclassOf(typeof(SettingsCategoryViewModel))
            || openedByMain.Contains(type)
            || TakesInput(type));
    }

    private static bool TakesInput(Type type) => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Any(method => method.Name.StartsWith("Handle", StringComparison.Ordinal)
            && method.GetParameters() is [{ ParameterType: var first }, ..] && first == typeof(UiAction));
}
