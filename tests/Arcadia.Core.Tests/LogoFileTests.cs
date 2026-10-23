using System.Text;
using Arcadia.Core.Launchers;

namespace Arcadia.Core.Tests;

public sealed class LogoFileTests
{
    private const string Svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">{0}</svg>""";

    private static LogoCheck CheckSvg(string inner) => LogoFile.Check(Encoding.UTF8.GetBytes(string.Format(Svg, inner)));

    [Fact]
    public void A_picture_is_told_by_its_content()
    {
        Assert.Equal(LogoFormat.Png, LogoFile.Check([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13]).Format);
        Assert.Equal(LogoFormat.WebP, LogoFile.Check("RIFF\x10\0\0\0WEBPVP8 "u8).Format);
        Assert.Equal(LogoFormat.Ico, LogoFile.Check([0, 0, 1, 0, 1, 0, 16, 16, 0, 0]).Format);
        Assert.Equal(LogoFormat.Svg, CheckSvg("""<path d="M0 0h24v24H0z" fill="url(#g)"/><use href="#p"/>""").Format);
    }

    [Fact]
    public void An_svg_may_start_with_a_declaration_a_comment_and_a_byte_order_mark() =>
        Assert.True(LogoFile.Check(Encoding.UTF8.GetBytes("\uFEFF<?xml version=\"1.0\"?>\n<!-- logo -->\n" + string.Format(Svg, "<path d=\"M0 0\"/>"))).IsValid);

    [Theory]
    [InlineData("GIF89a")]
    [InlineData("<html><body>Kein Bild</body></html>")]
    [InlineData("<svg")]
    [InlineData("Ein Text")]
    public void Anything_else_is_no_logo(string content)
    {
        var check = LogoFile.Check(Encoding.UTF8.GetBytes(content));

        Assert.False(check.IsValid);
        Assert.Equal("Das ist keine PNG-, SVG-, ICO- oder WebP-Datei.", check.Problem);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<foreignObject><div xmlns=\"http://www.w3.org/1999/xhtml\">x</div></foreignObject>")]
    [InlineData("<image href=\"file:///C:/Windows/win.ini\"/>")]
    [InlineData("<use href=\"https://example.com/logo.svg#a\"/>")]
    [InlineData("<use xmlns:xlink=\"http://www.w3.org/1999/xlink\" xlink:href=\"other.svg#a\"/>")]
    [InlineData("<rect fill=\"url(https://example.com/a.png)\"/>")]
    [InlineData("<style>@import url(https://example.com/a.css);</style>")]
    [InlineData("<style>rect { fill: url('https://example.com/x') }</style>")]
    [InlineData("<rect onload=\"alert(1)\"/>")]
    public void An_svg_that_runs_or_loads_something_is_refused(string inner) =>
        Assert.False(CheckSvg(inner).IsValid);

    [Fact]
    public void An_svg_with_a_document_type_is_refused()
    {
        var svg = """<?xml version="1.0"?><!DOCTYPE svg [<!ENTITY a "aaaaaaaaaa">]><svg xmlns="http://www.w3.org/2000/svg">&a;</svg>""";

        Assert.False(LogoFile.Check(Encoding.UTF8.GetBytes(svg)).IsValid);
    }

    [Fact]
    public void An_empty_or_too_big_file_is_refused_with_its_reason()
    {
        Assert.Equal("Die Datei ist leer.", LogoFile.Check([]).Problem);
        Assert.Equal("Die Datei ist größer als 2 MB.", LogoFile.SizeProblem(LogoFile.MaxBytes + 1));
        Assert.Null(LogoFile.SizeProblem(LogoFile.MaxBytes));
    }
}
