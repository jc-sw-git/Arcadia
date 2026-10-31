using Arcadia.ControllerSupport.Pan;
using Arcadia.Input;
using Arcadia.Pan;

namespace Arcadia.Core.Tests;

/// <summary>"Controller zuordnen" (#238 D3a): the stored choices per model, read from the settings file.</summary>
public sealed class ControllerLayoutsTests
{
    private static readonly ControllerModel Model = new(0x054C, 0x0CE6);

    [Theory]
    [InlineData("DualSense", PadKind.DualSense)]
    [InlineData("Switch2Pro", PadKind.Switch2Pro)]
    [InlineData("Xbox360", PadKind.Xbox360)]
    public void A_stored_kind_is_read_back(string value, PadKind kind) =>
        Assert.Equal(ControllerLayout.Of(kind), ControllerLayouts.Find(new Dictionary<string, string> { ["054C:0CE6"] = value }, Model));

    [Fact]
    public void Not_taken_over_is_read_back() =>
        Assert.Equal(ControllerLayout.NotTakenOver, ControllerLayouts.Find(new Dictionary<string, string> { ["054C:0CE6"] = "NotTakenOver" }, Model));

    [Theory]
    [InlineData("")]
    [InlineData("2")]
    [InlineData("99")]
    [InlineData("dualsense")]
    [InlineData("Gamecube")]
    [InlineData("DualSense, Xbox360")]
    public void Anything_else_in_the_file_counts_as_no_choice(string value) =>
        Assert.Null(ControllerLayouts.Find(new Dictionary<string, string> { ["054C:0CE6"] = value }, Model));

    [Fact]
    public void No_entry_or_no_dictionary_is_no_choice()
    {
        Assert.Null(ControllerLayouts.Find(null, Model));
        Assert.Null(ControllerLayouts.Find(new Dictionary<string, string> { ["054C:0CE7"] = "Xbox360" }, Model));
    }

    [Fact]
    public void Every_choice_survives_a_round_trip()
    {
        foreach (var layout in ControllerLayouts.Choices(PadKind.DualSenseEdge))
        {
            Assert.Equal(layout, ControllerLayouts.Find(new Dictionary<string, string> { [ControllerLayouts.Key(Model)] = ControllerLayouts.Value(layout) }, Model));
        }
    }

    [Fact]
    public void The_choices_cycle_through_the_kinds_then_not_taken_over_and_name_the_Edge_only_for_an_Edge()
    {
        Assert.Equal(
            ["Xbox 360 Controller", "DualShock 4", "DualSense", "Switch 2 Pro Controller", "Nicht übernehmen"],
            ControllerLayouts.Choices(PadKind.Xbox360).Select(ControllerLayouts.DisplayName));
        Assert.Equal(
            ["Xbox 360 Controller", "DualShock 4", "DualSense", "DualSense Edge", "Switch 2 Pro Controller", "Nicht übernehmen"],
            ControllerLayouts.Choices(PadKind.DualSenseEdge).Select(ControllerLayouts.DisplayName));
    }

    [Fact]
    public void A_pad_is_the_chosen_kind_where_the_model_may_be_it_else_its_own()
    {
        Assert.Equal(PadKind.Switch2Pro, ControllerLayouts.KindFor(ControllerLayout.Of(PadKind.Switch2Pro), PadKind.Xbox360));
        Assert.Equal(PadKind.DualSense, ControllerLayouts.KindFor(ControllerLayout.Of(PadKind.DualSenseEdge), PadKind.DualSense));
        Assert.Equal(PadKind.DualSense, ControllerLayouts.KindFor(null, PadKind.DualSense));
        Assert.Equal(PadKind.DualSense, ControllerLayouts.KindFor(ControllerLayout.NotTakenOver, PadKind.DualSense));
    }
}
