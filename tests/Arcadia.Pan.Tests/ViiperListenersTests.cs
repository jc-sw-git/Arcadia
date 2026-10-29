using System.Net;
using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>#304: what Arcadia's viiper.exe must listen on to be trusted, checked in both IPv4 and IPv6.</summary>
public sealed class ViiperListenersTests
{
    private const int Viiper = 42;

    private static TcpListening Own(int port, string address = "127.0.0.1") => new(Viiper, IPAddress.Parse(address), port);

    [Fact]
    [Trait("TestCase", "T304-1")]
    public void Two_ports_on_127_0_0_1_and_nobody_else_on_them_are_trusted()
    {
        var (ports, problem) = ViiperListeners.OfServer([Own(50001), Own(50002), new(7, IPAddress.Any, 80)], Viiper);

        Assert.Null(problem);
        Assert.Equal([50001, 50002], ports!);
    }

    [Fact]
    [Trait("TestCase", "T304-1")]
    public void One_port_so_far_is_neither_trusted_nor_a_problem_yet()
    {
        Assert.Equal((null, null), ViiperListeners.OfServer([Own(50001)], Viiper));
    }

    [Theory]
    [Trait("TestCase", "T304-5")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("192.168.1.5")]
    public void Any_address_but_127_0_0_1_is_a_problem(string address)
    {
        var (ports, problem) = ViiperListeners.OfServer([Own(50001), Own(50002, address)], Viiper);

        Assert.Null(ports);
        Assert.Contains("not only on 127.0.0.1", problem);
    }

    [Fact]
    [Trait("TestCase", "T304-5")]
    public void A_third_port_is_a_problem()
    {
        var (ports, problem) = ViiperListeners.OfServer([Own(50001), Own(50002), Own(50003)], Viiper);

        Assert.Null(ports);
        Assert.Contains("3 ports instead of 2", problem);
    }

    [Theory]
    [Trait("TestCase", "T304-5")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    public void Another_process_on_the_same_port_number_in_either_family_is_a_problem(string address)
    {
        var (ports, problem) = ViiperListeners.OfServer([Own(50001), Own(50002), new(7, IPAddress.Parse(address), 50002)], Viiper);

        Assert.Null(ports);
        Assert.Contains("process 7 listens on port 50002 too", problem);
    }

    [Fact]
    [Trait("TestCase", "T304-6")]
    public void A_port_is_served_only_when_a_held_process_listens_there_and_nobody_else()
    {
        Assert.Null(ViiperListeners.OnlyServedBy([Own(50001)], 50001, [Viiper]));
        Assert.Contains("no held viiper.exe", ViiperListeners.OnlyServedBy([Own(50002)], 50001, [Viiper]));
        Assert.Contains("process 7", ViiperListeners.OnlyServedBy([Own(50001), new(7, IPAddress.IPv6Any, 50001)], 50001, [Viiper]));
    }
}
