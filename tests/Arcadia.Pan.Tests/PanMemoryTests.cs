using System.Text.Json;

namespace Arcadia.Pan.Tests;

/// <summary>
/// Pan's memory budget (#360, T243-21: under 20 MB). Pan's own memory is only measurable on Windows; what keeps it small
/// is the GC's gen-0 budget, set in Pan's project. Native AOT reads the same option at publish, so this guards both builds.
/// </summary>
public class PanMemoryTests
{
    private const long MaxGen0Budget = 4 * 1024 * 1024;

    [Fact]
    public void Pan_collects_its_garbage_before_it_piles_up_to_the_size_of_the_cpu_cache()
    {
        // The test's output holds Pan's runtimeconfig, as Pan is a project reference.
        var path = Path.Combine(AppContext.BaseDirectory, "Pan.runtimeconfig.json");
        using var config = JsonDocument.Parse(File.ReadAllText(path));
        var properties = config.RootElement.GetProperty("runtimeOptions").GetProperty("configProperties");

        Assert.True(properties.TryGetProperty("System.GC.Gen0MaxBudget", out var budget), "Pan sets no gen-0 budget");
        Assert.InRange(budget.GetInt64(), 1, MaxGen0Budget);
    }
}
