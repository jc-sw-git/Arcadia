using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>
/// #565 "Bild ansehen" by itself: when it starts and turns, the random order without repeats, images that can't be shown,
/// and the foreground. The flows through the window's input are in <see cref="ImageShowAcceptanceTests"/>.
/// </summary>
public sealed class ImageShowTests
{
    private static readonly TimeSpan Idle = ImageShowViewModel.IdleTime;

    private readonly FakeTimeProvider _time = new();
    private readonly HashSet<string> _broken = [];
    private bool _mayRun = true;
    private ImageShowScene _scene = new(Image("own"), [Image("a"), Image("b"), Image("c")]);

    [Fact]
    [Trait("TestCase", "T565-6")]
    public void It_starts_with_the_own_image_after_ten_seconds_without_input_and_input_meanwhile_starts_the_wait_anew()
    {
        using var show = Create();
        show.TakeInput();

        _time.Advance(Idle - TimeSpan.FromSeconds(1));
        Assert.False(show.TakeInput());
        _time.Advance(Idle - TimeSpan.FromSeconds(1));
        Assert.False(show.IsShowing);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(show.IsShowing);
        Assert.Equal("own", show.Shown?.GameKey);
    }

    [Fact]
    [Trait("TestCase", "T565-7")]
    public void Every_ten_seconds_another_game_blends_in_and_none_comes_twice_before_all_were_shown()
    {
        using var show = Create();
        show.TakeInput();
        _time.Advance(Idle);

        var seen = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            var layerBefore = show.ShowsFirst;
            _time.Advance(Idle);
            Assert.NotEqual(layerBefore, show.ShowsFirst);
            seen.Add(show.Shown!.GameKey);
        }

        Assert.Equal(["a", "b", "c"], seen[..3].Order());
        Assert.Equal(["a", "b", "c"], seen[3..].Order());
        Assert.DoesNotContain("own", seen);
        for (var i = 1; i < seen.Count; i++)
        {
            Assert.NotEqual(seen[i - 1], seen[i]);
        }
    }

    [Fact]
    [Trait("TestCase", "T565-8")]
    public void Input_ends_the_show_says_so_once_and_the_images_go_after_the_fade()
    {
        using var show = Create();
        show.TakeInput();
        _time.Advance(Idle);

        Assert.True(show.TakeInput());
        Assert.False(show.IsShowing);
        Assert.False(show.EndsAtOnce);
        Assert.True(show.IsPresent);
        Assert.False(show.TakeInput());

        _time.Advance(ImageShowViewModel.FadeTime);
        Assert.False(show.IsPresent);
        Assert.Null(show.Shown);
    }

    [Fact]
    [Trait("TestCase", "T565-8")]
    public void A_click_or_the_wheel_while_the_page_fades_back_in_does_nothing_either()
    {
        using var show = Create();
        show.TakeInput();
        _time.Advance(Idle);

        // The mouse moved: the show ends and the page fades back in under the image.
        Assert.True(show.TakeInput());
        Assert.True(show.TakePointerInput());
        Assert.False(show.IsShowing);

        _time.Advance(ImageShowViewModel.FadeTime);
        Assert.False(show.TakePointerInput());
    }

    [Fact]
    [Trait("TestCase", "T565-14")]
    public void The_next_image_goes_into_an_emptied_layer_so_the_view_blends_it_in_only_once_it_is_decoded()
    {
        _scene = new(Image("own"), [Image("a"), Image("b")]);
        using var show = Create();
        show.TakeInput();
        _time.Advance(Idle);
        _time.Advance(Idle);
        var inSecond = show.SecondImage!.GameKey;
        _time.Advance(Idle);

        // The round starts again with the image the second layer still holds: it is emptied first, so it loads anew.
        var changes = new List<string?>();
        show.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ImageShowViewModel.SecondImage))
            {
                changes.Add(show.SecondImage?.GameKey);
            }
        };
        _time.Advance(Idle);

        Assert.Equal(inSecond, show.Shown?.GameKey);
        Assert.Equal([null, inSecond], changes);
    }

    [Fact]
    [Trait("TestCase", "T565-9")]
    public void It_never_starts_while_it_may_not_run_and_ends_when_a_dialog_opens_over_it()
    {
        using var show = Create();
        _mayRun = false;
        show.TakeInput();
        _time.Advance(Idle * 3);
        Assert.False(show.IsShowing);

        _mayRun = true;
        show.Restart();
        _time.Advance(Idle);
        Assert.True(show.IsShowing);

        _mayRun = false;
        show.Restart();
        Assert.False(show.IsShowing);
    }

    [Fact]
    [Trait("TestCase", "T565-10")]
    public void In_the_background_no_timer_runs_the_show_ends_at_once_and_back_in_front_the_wait_starts_anew()
    {
        using var show = Create();
        show.TakeInput();
        _time.Advance(Idle);
        Assert.True(show.IsShowing);

        show.SetInFront(false);
        Assert.False(show.IsShowing);
        Assert.True(show.EndsAtOnce);
        Assert.False(show.IsPresent);

        _time.Advance(Idle * 3);
        Assert.False(show.IsShowing);

        show.SetInFront(true);
        _time.Advance(Idle - TimeSpan.FromSeconds(1));
        Assert.False(show.IsShowing);
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(show.IsShowing);
        Assert.False(show.EndsAtOnce);
    }

    [Fact]
    [Trait("TestCase", "T565-11")]
    public void Without_another_game_with_a_background_the_own_image_stays()
    {
        _scene = new ImageShowScene(Image("own"), []);
        using var show = Create();
        show.TakeInput();
        for (var i = 0; i < 4; i++)
        {
            _time.Advance(Idle);
        }

        Assert.True(show.IsShowing);
        Assert.Equal("own", show.Shown?.GameKey);
    }

    [Fact]
    [Trait("TestCase", "T565-13")]
    public void An_image_that_cant_be_shown_is_skipped_and_the_next_one_taken()
    {
        _broken.Add("a");
        _broken.Add("b");
        using var show = Create();
        show.TakeInput();
        _time.Advance(Idle);
        _time.Advance(Idle);

        Assert.Equal("c", show.Shown?.GameKey);

        // A game gone from the tab (a rescan) is never picked again.
        _scene = new ImageShowScene(Image("own"), [Image("d")]);
        _time.Advance(Idle);
        Assert.Equal("d", show.Shown?.GameKey);
    }

    [Fact]
    public void Without_an_own_background_it_starts_with_another_games_and_without_any_it_doesnt_start()
    {
        _scene = new ImageShowScene(null, [Image("a")]);
        using (var show = Create())
        {
            show.TakeInput();
            _time.Advance(Idle);
            Assert.Equal("a", show.Shown?.GameKey);
        }

        _scene = new ImageShowScene(null, []);
        using (var show = Create())
        {
            show.TakeInput();
            _time.Advance(Idle);
            _time.Advance(Idle);
            Assert.False(show.IsShowing);
        }
    }

    [Fact]
    public void The_order_never_repeats_the_current_image_and_gives_nothing_without_another()
    {
        var order = new ImageShowOrder(new Random(7));
        Assert.Null(order.Next([Image("a")], current: "a"));
        Assert.Null(order.Next([], current: null));
        Assert.Equal("b", order.Next([Image("a"), Image("b")], current: "a")?.GameKey);
        Assert.Equal("a", order.Next([Image("a"), Image("b")], current: "b")?.GameKey);
    }

    private ImageShowViewModel Create() => new(_time, action => action(), () => _mayRun, () => _scene,
        file => Task.FromResult(!_broken.Contains(file.GameKey)), new Random(565));

    private static ShownImage Image(string key) =>
        new(key, $"Spiel {key}", new ArtworkFile(key, ArtworkKind.Hero, $"/made-up/{key}.png", new ImageSize(1920, 620), 1));
}
