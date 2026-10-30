using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

public class PanMessageTests
{
    [Fact]
    public void A_message_survives_the_wire_unchanged()
    {
        var message = PanMessage.Of(PanCommand.Heartbeat, generation: 0xDEADBEEF, value: 7);
        var bytes = message.ToBytes();

        Assert.Equal(PanMessage.Size, bytes.Length);
        Assert.Equal(message, PanMessage.Read(bytes));
        Assert.Equal(PanMessage.ProtocolVersion, PanMessage.Read(bytes).Version);
    }

    [Fact]
    public void Replies_and_commands_are_told_apart_by_the_high_bit()
    {
        var reply = PanMessage.Of(PanReply.TornDown, 3, PanReason.Hang);
        Assert.Equal(PanReply.TornDown, reply.Reply);
        Assert.Equal(PanReason.Hang, reply.Reason);
        Assert.Equal("TornDown(gen 3, Hang)", reply.ToString());
        Assert.Equal("Hello(gen 0, v2)", PanMessage.Of(PanCommand.Hello).ToString());
    }
}
