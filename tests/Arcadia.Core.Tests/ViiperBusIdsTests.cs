using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

/// <summary>
/// Every pad and copy on its own bus id (#331): usbip-win2 attaches a removed device again by its bus id, so an id
/// served again gives Steam the new device twice.
/// </summary>
public sealed class ViiperBusIdsTests
{
    [Fact]
    [Trait("TestCase", "T331-2")]
    public void A_bus_id_is_never_handed_out_twice_not_even_to_plugs_at_the_same_time()
    {
        var ids = new System.Collections.Concurrent.ConcurrentBag<uint>();

        Parallel.For(0, 10_000, _ => ids.Add(ViiperBusIds.Next()));

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    [Trait("TestCase", "T331-2")]
    public void A_bus_id_is_never_0_which_VIIPER_reads_as_its_lowest_free_bus()
    {
        Assert.DoesNotContain(0u, Enumerable.Range(0, 1_000).Select(_ => ViiperBusIds.Next()));
    }
}
