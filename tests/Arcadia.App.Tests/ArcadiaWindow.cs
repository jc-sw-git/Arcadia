using Arcadia.App.Views;
using Arcadia.Core.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// Arcadia's real window on the headless platform, drawn by Skia (#191): what the user sees and does with the mouse
/// and a real keyboard. The controller and the clock stay with <see cref="Arcadia"/>; this class adds clicks, keys,
/// where a control stands and the colour of a pixel. Positions are in window pixels at a scaling of 1.
/// </summary>
internal sealed class ArcadiaWindow : IDisposable
{
    /// <summary>Big Picture's own size; other sizes scale it in a <c>Viewbox</c>.</summary>
    public const int Width = 1920;

    public const int Height = 1080;

    /// <summary>
    /// The longest transition of a fade or slide (the detail page's 0.34 s) and a little more. Transitions run on the
    /// headless render clock, which is real time, not the driver's fake clock.
    /// </summary>
    private static readonly TimeSpan TransitionTime = TimeSpan.FromMilliseconds(400);

    /// <summary>Nothing happened since the transitions last ran to their end.</summary>
    private bool _settled;

    private readonly bool _ownsArcadia;

    /// <summary>The window handled the last key press, e.g. as an action.</summary>
    private bool _keyHandled;

    private ArcadiaWindow(ArcadiaDriver arcadia, MainWindow window, bool ownsArcadia = true)
    {
        _ownsArcadia = ownsArcadia;
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => _keyHandled |= e.Handled, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        Arcadia = arcadia;
        Window = window;
    }

    public ArcadiaDriver Arcadia { get; }

    public MainWindow Window { get; }

    /// <summary>
    /// Arcadia started as in <see cref="ArcadiaDriver.StartAsync"/>, its window shown at <paramref name="width"/> × <paramref name="height"/>;
    /// <paramref name="steam"/> are Windows, Steam and SISR of the test, as there.
    /// </summary>
    public static async Task<ArcadiaWindow> ShowAsync(
        IReadOnlyList<LauncherSpec>? launchers = null, AppSettings? settings = null, int width = Width, int height = Height, double scaling = 1, FakeSteam? steam = null)
    {
        var arcadia = await ArcadiaDriver.StartAsync(launchers, settings, steam);
        // The glyphs follow one switch for the whole app; a test before may have left it on the keyboard.
        Input.InputHints.UsesController = arcadia.Main.UsesController;
        var window = new MainWindow { DataContext = arcadia.Main, Width = width, Height = height };
        window.SetRenderScaling(scaling);
        window.Show();
        var shown = new ArcadiaWindow(arcadia, window);
        shown.Settle();
        return shown;
    }

    /// <summary>Arcadia closed and started again on the same settings, its window shown anew; dispose both.</summary>
    public async Task<ArcadiaWindow> RestartAsync()
    {
        Window.Close();
        Dispatcher.UIThread.RunJobs();
        var restarted = await Arcadia.RestartAsync();
        Input.InputHints.UsesController = restarted.Main.UsesController;
        var window = new MainWindow { DataContext = restarted.Main, Width = Window.Width, Height = Window.Height };
        window.Show();
        var shown = new ArcadiaWindow(restarted, window, ownsArcadia: false);
        shown.Settle();
        return shown;
    }

    /// <summary>Settings as after the first start with <paramref name="pageSize"/> games per page.</summary>
    public static AppSettings WithPageSize(int pageSize, IReadOnlyList<LauncherSpec>? launchers = null) => new()
    {
        ActiveLaunchers = (launchers ?? ArcadiaDriver.DefaultLaunchers).Select(spec => spec.Launcher.Id).ToList(),
        PreferredPageSize = pageSize,
    };

    /// <summary>Lets queued UI work and layout finish; transitions have only begun.</summary>
    public ArcadiaWindow Settle()
    {
        _settled = false;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return this;
    }

    /// <summary>Waits until every fade and slide has run to its end, as a moment passing for the user.</summary>
    public ArcadiaWindow WaitForTransitions()
    {
        if (_settled)
        {
            return this;
        }

        // A transition takes its start from the first frame after the change, and jumps to its end on a frame after its time.
        Settle();
        Thread.Sleep(TransitionTime);
        Settle();
        _settled = true;
        return this;
    }

    /// <summary>A controller button tapped, as <see cref="ArcadiaDriver.Press"/>, then the window settles.</summary>
    public ArcadiaWindow Press(Input.UiAction action, int times = 1)
    {
        Arcadia.Press(action, times);
        return Settle();
    }

    // ---- Mouse ----

    public ArcadiaWindow Click(Control control, MouseButton button = MouseButton.Left) => Click(CentreOf(control), button);

    public ArcadiaWindow Click(Point point, MouseButton button = MouseButton.Left)
    {
        MouseDown(point, button);
        return MouseUp(point, button);
    }

    public ArcadiaWindow DoubleClick(Control control)
    {
        var point = CentreOf(control);
        return Click(point).Click(point);
    }

    public ArcadiaWindow MouseDown(Point point, MouseButton button = MouseButton.Left)
    {
        Window.MouseMove(point);
        Window.MouseDown(point, button);
        return Settle();
    }

    public ArcadiaWindow MouseUp(Point point, MouseButton button = MouseButton.Left)
    {
        Window.MouseUp(point, button);
        return Settle();
    }

    public ArcadiaWindow MouseMove(Point point)
    {
        Window.MouseMove(point);
        return Settle();
    }

    /// <summary>The wheel turned by <paramref name="notches"/>; positive turns it away from the user (up).</summary>
    public ArcadiaWindow Wheel(Control over, int notches)
    {
        Window.MouseWheel(CentreOf(over), new Vector(0, notches));
        return Settle();
    }

    // ---- Keyboard ----

    /// <summary>A key pressed and let go at once, with its text where it types one (letters, digits, space).</summary>
    public ArcadiaWindow Key(Key key, KeyModifiers modifiers = KeyModifiers.None, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            KeyDown(key, modifiers);
            KeyUp(key, modifiers);
        }

        return this;
    }

    public ArcadiaWindow KeyDown(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        _keyHandled = false;
        Window.KeyPress(key, (RawInputModifiers)modifiers, PhysicalKey.None, TextOf(key, modifiers));

        // As on Windows, a key whose press was handled types nothing (Avalonia drops its WM_CHAR).
        if (!_keyHandled && TextOf(key, modifiers) is { } text)
        {
            Window.KeyTextInput(text);
        }

        return Settle();
    }

    public ArcadiaWindow KeyUp(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Window.KeyRelease(key, (RawInputModifiers)modifiers, PhysicalKey.None, TextOf(key, modifiers));
        return Settle();
    }

    /// <summary>
    /// <paramref name="text"/> typed on a real keyboard, character by character: the key goes down, its character arrives
    /// unless the press was handled, the key goes up. Characters without a key of their own here (/, umlauts) come
    /// with a key Arcadia doesn't map, as on a German keyboard.
    /// </summary>
    public ArcadiaWindow Type(string text)
    {
        foreach (var character in text)
        {
            var (key, modifiers) = KeyOf(character);
            _keyHandled = false;
            Window.KeyPress(key, (RawInputModifiers)modifiers, PhysicalKey.None, character.ToString());
            if (!_keyHandled)
            {
                Window.KeyTextInput(character.ToString());
            }

            Window.KeyRelease(key, (RawInputModifiers)modifiers, PhysicalKey.None, character.ToString());
            Settle();
        }

        return this;
    }

    private static (Key Key, KeyModifiers Modifiers) KeyOf(char character) => character switch
    {
        >= 'a' and <= 'z' => (Avalonia.Input.Key.A + (character - 'a'), KeyModifiers.None),
        >= 'A' and <= 'Z' => (Avalonia.Input.Key.A + (character - 'A'), KeyModifiers.Shift),
        >= '0' and <= '9' => (Avalonia.Input.Key.D0 + (character - '0'), KeyModifiers.None),
        ' ' => (Avalonia.Input.Key.Space, KeyModifiers.None),
        _ => (Avalonia.Input.Key.OemQuestion, KeyModifiers.None),
    };

    private static string? TextOf(Key key, KeyModifiers modifiers) => modifiers is KeyModifiers.None or KeyModifiers.Shift
        ? key switch
        {
            >= Avalonia.Input.Key.A and <= Avalonia.Input.Key.Z => modifiers == KeyModifiers.Shift ? key.ToString() : key.ToString().ToLowerInvariant(),
            >= Avalonia.Input.Key.D0 and <= Avalonia.Input.Key.D9 => ((char)('0' + (key - Avalonia.Input.Key.D0))).ToString(),
            Avalonia.Input.Key.Space => " ",
            _ => null,
        }
        : null;

    // ---- What the window shows ----

    /// <summary>A named control of the window, e.g. <c>GridScroller</c>.</summary>
    public T Named<T>(string name)
        where T : Control => Window.FindControl<T>(name) ?? throw new InvalidOperationException($"Kein {typeof(T).Name} namens {name} im Fenster.");

    /// <summary>The visible button whose tooltip begins with <paramref name="tip"/>, e.g. "Nächste Seite".</summary>
    public Button ButtonWithTip(string tip) => Assert.Single(
        Window.GetVisualDescendants().OfType<Button>(),
        button => button.IsEffectivelyVisible && ToolTip.GetTip(button) is string text && text.StartsWith(tip, StringComparison.Ordinal));

    /// <summary>The cover of <paramref name="tile"/> in the library's grid.</summary>
    public Button Tile(ViewModels.GameTileViewModel tile) =>
        (Named<ItemsControl>("GameGrid").ContainerFromItem(tile) as Control)?.GetVisualDescendants().OfType<Button>().FirstOrDefault()
        ?? throw new InvalidOperationException($"{tile.Title} steht nicht im Raster.");

    /// <summary>
    /// Fails unless a focus ring of <paramref name="ring"/> (a <c>BoxShadows</c> key of <c>Theme.axaml</c>) is drawn
    /// along all four sides of <paramref name="control"/>, i.e. nothing clips or covers it.
    /// </summary>
    public void AssertRingAround(Control control, string where, string ring = "FocusRing") => AssertRingAround(Window, Capture, control, where, ring);

    /// <summary>As <see cref="AssertRingAround(Control, string, string)"/>, in another window, e.g. the mouse mode's keyboard.</summary>
    public static void AssertRingAround(Window window, Control control, string where, string ring = "FocusRing") =>
        AssertRingAround(window, () => CaptureSettled(window), control, where, ring);

    private static void AssertRingAround(Window window, Func<Frame> capture, Control control, string where, string ring)
    {
        var shadow = ThemeResource<BoxShadows>(ring);
        // The ring is the widest sharp outline; a narrower one painted over it (FocusRingGap) leaves a gap inside it.
        var outlines = Enumerable.Range(0, shadow.Count).Select(i => shadow[i]).Where(s => s.Blur == 0 && !s.IsInset).OrderBy(s => s.Spread).ToList();
        var outline = outlines[^1];
        // Captured first: the capture waits for transitions (the cover's lift), and the bounds must be those it shows.
        using var frame = capture();
        var bounds = Bounds(window, control);
        var reach = (outline.Spread + 1) * bounds.Width / control.Bounds.Width;
        // Rounded corners bend the ring inwards: the sides are looked at in their middle, top and bottom along their
        // straight part too.
        var along = bounds.Width > 2 * bounds.Height ? new[] { 0.3, 0.5, 0.7 } : [0.5];
        var probes = along.SelectMany(fraction => new[]
            {
                ("oben", new Point(bounds.Left + (bounds.Width * fraction), bounds.Top), new Vector(0, -1)),
                ("unten", new Point(bounds.Left + (bounds.Width * fraction), bounds.Bottom), new Vector(0, 1)),
            })
            .Append(("links", new Point(bounds.Left, bounds.Center.Y), new Vector(-1, 0)))
            .Append(("rechts", new Point(bounds.Right, bounds.Center.Y), new Vector(1, 0)));
        foreach (var (side, edge, step) in probes)
        {
            // From the control's edge outwards, as far as the ring reaches: somewhere there it must show.
            var found = Enumerable.Range(0, (int)Math.Ceiling(reach * frame.Scaling) + 1)
                .Select(i => frame.At(edge + (step * i / frame.Scaling)))
                .Any(colour => Distance(colour, outline.Color) <= 40);
            Assert.True(found, $"{where}: Fokusrahmen {side} bei {edge} fehlt oder ist abgeschnitten.");
        }
    }

    /// <summary>Nothing lies over <paramref name="control"/>: a click anywhere on it reaches it or something inside it.</summary>
    public void AssertOnTop(Control control)
    {
        var bounds = Bounds(control).Deflate(4);
        foreach (var x in new[] { 0.1, 0.5, 0.9 })
        {
            foreach (var y in new[] { 0.05, 0.5, 0.95 })
            {
                var point = new Point(bounds.Left + (bounds.Width * x), bounds.Top + (bounds.Height * y));
                var hit = Window.InputHitTest(point) as Visual;
                Assert.True(hit is not null && hit.GetSelfAndVisualAncestors().Contains(control), $"Bei {point} liegt {hit} über {control}");
            }
        }
    }

    /// <summary>
    /// The button hints shown in the footer, left to right, as the user reads them: the buttons or keys, then the text,
    /// e.g. "LB RB Launcher" or "Leertaste / Klick Auswählen".
    /// </summary>
    public IReadOnlyList<string> FooterHints() => Hints(Window.GetVisualDescendants().OfType<Controls.KeyHint>()
        .Where(hint => hint.IsEffectivelyVisible && Bounds(hint).Top > Bounds(Window.Content as Control ?? Window).Bottom - 80));

    /// <summary>The hints of <paramref name="hints"/>, left to right, as in <see cref="FooterHints"/>.</summary>
    public IReadOnlyList<string> Hints(IEnumerable<Controls.KeyHint> hints) => hints
        .OrderBy(hint => Bounds(hint).Left)
        .Select(hint => string.Join(' ', hint.Children.Select(child => child switch
        {
            Border { Child: TextBlock glyph } => glyph.Text,
            TextBlock text => text.Text,
            _ => null,
        }).Where(text => !string.IsNullOrEmpty(text))))
        .ToList();

    /// <summary>The glyphs (button or key frames) drawn inside <paramref name="within"/>.</summary>
    public static IReadOnlyList<Border> Glyphs(Control within) => within.GetVisualDescendants().OfType<Border>()
        .Where(border => border.Classes.Contains("glyph") && border.IsEffectivelyVisible)
        .ToList();

    /// <summary>How much larger than its own size a control is drawn (Viewbox, lift).</summary>
    public double ScaleOf(Control control) => Bounds(control).Width / control.Bounds.Width;

    public static int Distance(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    /// <summary>The outermost visible control that shows <paramref name="item"/>, e.g. the cover of a tile.</summary>
    public Control Showing(object item) => ShowingAll(item).FirstOrDefault()
        ?? throw new InvalidOperationException($"Nichts im Fenster zeigt {item}.");

    public IEnumerable<Control> ShowingAll(object item) => Window.GetVisualDescendants()
        .OfType<Control>()
        .Where(control => ReferenceEquals(control.DataContext, item) && control.IsEffectivelyVisible
            && (control.GetVisualParent() as Control)?.DataContext != item);

    /// <summary>Visible controls of type <typeparamref name="T"/> whose text or content says <paramref name="text"/>.</summary>
    public IReadOnlyList<Control> WithText(string text) => Window.GetVisualDescendants()
        .OfType<TextBlock>()
        .Where(block => block.IsEffectivelyVisible && block.Text == text && Bounds(block).Width > 0)
        .Cast<Control>()
        .ToList();

    public Control OneWithText(string text) => Assert.Single(WithText(text));

    /// <summary>Where <paramref name="control"/> stands in the window, after render transforms (a slide, a lift).</summary>
    public Rect Bounds(Control control) => Bounds(Window, control);

    public static Rect Bounds(Window window, Control control) =>
        control.TransformToVisual(window) is { } transform
            ? new Rect(control.Bounds.Size).TransformToAABB(transform)
            : throw new InvalidOperationException($"{control} steht nicht im Fenster.");

    /// <summary>Where <paramref name="control"/> stands without its own render transform, e.g. a button at rest while pressed.</summary>
    public Rect LayoutBounds(Control control) =>
        control.GetVisualParent() is Visual parent && parent.TransformToVisual(Window) is { } transform
            ? control.Bounds.TransformToAABB(transform)
            : throw new InvalidOperationException($"{control} steht nicht im Fenster.");

    /// <summary>
    /// The middle of the part of <paramref name="control"/> inside the window, where a click reaches it: once it stands
    /// still, as the user aims at it, not where a slide or lift has it now.
    /// </summary>
    public Point CentreOf(Control control) => WaitForTransitions().Bounds(control).Intersect(new Rect(Window.ClientSize)).Center;

    /// <summary>A colour of <c>Theme.axaml</c>, e.g. <c>BgColor</c>.</summary>
    public static Color ThemeColour(string key) => ThemeResource<Color>(key);

    public static T ThemeResource<T>(string key) =>
        Application.Current!.TryGetResource(key, Avalonia.Styling.ThemeVariant.Dark, out var value) && value is T resource
            ? resource
            : throw new InvalidOperationException($"Kein {typeof(T).Name} {key} in Theme.axaml.");

    /// <summary>The window as it is drawn now.</summary>
    /// <summary><paramref name="window"/> as drawn once its transitions have run to their end.</summary>
    public static Frame CaptureSettled(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(TransitionTime);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Das Fenster hat nichts gezeichnet.");
        return new Frame(bitmap, window.RenderScaling);
    }

    public Frame Capture()
    {
        WaitForTransitions();
        var bitmap = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Das Fenster hat nichts gezeichnet.");
        return new Frame(bitmap, Window.RenderScaling);
    }

    public void Dispose()
    {
        Window.Close();
        Dispatcher.UIThread.RunJobs();
        if (_ownsArcadia)
        {
            Arcadia.Dispose();
        }
    }
}

/// <summary>One drawn frame of the window, read pixel by pixel in window coordinates.</summary>
internal sealed class Frame : IDisposable
{
    private readonly WriteableBitmap _bitmap;
    private readonly double _scaling;

    /// <summary>Device pixels per window pixel.</summary>
    public double Scaling => _scaling;
    private readonly int[] _pixels;
    private readonly int _width;
    private readonly bool _isRgba;

    public Frame(WriteableBitmap bitmap, double scaling)
    {
        _bitmap = bitmap;
        _scaling = scaling;
        using var locked = bitmap.Lock();
        _width = locked.Size.Width;
        Height = locked.Size.Height;
        _pixels = new int[_width * Height];
        // Skia draws in the platform's byte order: BGRA on Windows, RGBA elsewhere.
        _isRgba = locked.Format == PixelFormat.Rgba8888;
        if (!(_isRgba || locked.Format == PixelFormat.Bgra8888) || locked.RowBytes != _width * 4)
        {
            throw new InvalidOperationException($"Unerwartetes Pixelformat {locked.Format}.");
        }

        System.Runtime.InteropServices.Marshal.Copy(locked.Address, _pixels, 0, _pixels.Length);
    }

    public int Height { get; }

    /// <summary>The colour at a point of the window.</summary>
    public Color At(Point point) => At((int)(point.X * _scaling), (int)(point.Y * _scaling));

    public Color At(int x, int y)
    {
        // Little-endian: the first byte in memory is the lowest of the int.
        var pixel = (uint)_pixels[(Math.Clamp(y, 0, Height - 1) * _width) + Math.Clamp(x, 0, _width - 1)];
        var (first, second, third, alpha) = ((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16), (byte)(pixel >> 24));
        return _isRgba ? Color.FromArgb(alpha, first, second, third) : Color.FromArgb(alpha, third, second, first);
    }

    /// <summary>The brightness (0–255) of a point, as the eye weighs the colours.</summary>
    public double Brightness(Point point)
    {
        var colour = At(point);
        return (0.299 * colour.R) + (0.587 * colour.G) + (0.114 * colour.B);
    }

    /// <summary>The brightest point of an area, e.g. whether anything light is drawn there.</summary>
    public double BrightestIn(Rect area)
    {
        var brightest = 0.0;
        for (var y = area.Top; y < area.Bottom; y += 1 / _scaling)
        {
            for (var x = area.Left; x < area.Right; x += 1 / _scaling)
            {
                brightest = Math.Max(brightest, Brightness(new Point(x, y)));
            }
        }

        return brightest;
    }

    /// <summary>Saved as PNG, to look at a failing test.</summary>
    public void Save(string path)
    {
        using var file = File.Create(path);
        _bitmap.Save(file, new PngBitmapEncoderOptions());
    }

    public void Dispose() => _bitmap.Dispose();
}
