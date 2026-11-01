using System.Text;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

/// <summary>
/// A crafted Steam file nested thousands of levels deep must end as a <see cref="FormatException"/>
/// (which the scanners already handle per file), not as a stack overflow that ends Arcadia.
/// </summary>
public class VdfNestingTests
{
    // Far beyond the limit and deep enough to overflow the stack if the limit were missing.
    private const int CraftedDepth = 100_000;

    [Fact]
    public void Text_vdf_nested_up_to_the_limit_is_read()
    {
        var root = TextVdf.Parse(NestedText(VdfMap.MaxDepth));

        Assert.Equal("1", Innermost(root, VdfMap.MaxDepth).GetString("leaf"));
    }

    [Theory]
    [InlineData(VdfMap.MaxDepth + 1)]
    [InlineData(CraftedDepth)]
    public void Text_vdf_nested_deeper_than_the_limit_is_rejected(int depth)
    {
        var error = Assert.Throws<FormatException>(() => TextVdf.Parse(NestedText(depth)));

        Assert.Contains("nested deeper", error.Message);
    }

    [Fact]
    public void Binary_vdf_nested_up_to_the_limit_is_read()
    {
        var root = BinaryVdf.Parse(NestedBinary(VdfMap.MaxDepth));

        Assert.Equal(1, Innermost(root, VdfMap.MaxDepth).GetInt("leaf"));
    }

    [Theory]
    [InlineData(VdfMap.MaxDepth + 1)]
    [InlineData(CraftedDepth)]
    public void Binary_vdf_nested_deeper_than_the_limit_is_rejected(int depth)
    {
        var error = Assert.Throws<FormatException>(() => BinaryVdf.Parse(NestedBinary(depth)));

        Assert.Contains("nested deeper", error.Message);
    }

    [Fact]
    public void Binary_vdf_cut_off_inside_a_nested_map_is_rejected()
    {
        var whole = NestedBinary(3);
        var cutOff = whole[..^4]; // the ends of the three nested maps and the root's end are gone

        Assert.Throws<FormatException>(() => BinaryVdf.Parse(cutOff));
    }

    [Fact]
    public void Binary_vdf_root_without_its_final_end_byte_is_still_read()
    {
        var whole = NestedBinary(1);

        var root = BinaryVdf.Parse(whole[..^1]);

        Assert.Equal(1, Innermost(root, 1).GetInt("leaf"));
    }

    [Fact]
    public void Binary_vdf_with_an_unknown_type_byte_is_rejected()
    {
        byte[] data = [0x00, (byte)'a', 0x00, 0x07, (byte)'b', 0x00, 0x08, 0x08];

        var error = Assert.Throws<FormatException>(() => BinaryVdf.Parse(data));

        Assert.Contains("0x07", error.Message);
    }

    private static string NestedText(int depth)
    {
        var text = new StringBuilder(depth * 8);
        for (var i = 0; i < depth; i++)
        {
            text.Append("\"m\" { ");
        }

        text.Append("\"leaf\" \"1\" ");
        text.Append('}', depth);
        return text.ToString();
    }

    private static byte[] NestedBinary(int depth)
    {
        var data = new List<byte>(depth * 3 + 16);
        for (var i = 0; i < depth; i++)
        {
            data.AddRange([0x00, (byte)'m', 0x00]);
        }

        data.AddRange([0x02, (byte)'l', (byte)'e', (byte)'a', (byte)'f', 0x00, 0x01, 0x00, 0x00, 0x00]);
        for (var i = 0; i <= depth; i++)
        {
            data.Add(0x08);
        }

        return [.. data];
    }

    private static VdfMap Innermost(VdfMap root, int depth)
    {
        var map = root;
        for (var i = 0; i < depth; i++)
        {
            map = map.GetMap("m") ?? throw new InvalidOperationException($"Level {i + 1} is missing.");
        }

        return map;
    }
}
