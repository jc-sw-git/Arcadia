using Arcadia.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>
/// A launcher's logo in a square box (#138), wherever Arcadia names the launcher: the user's picture, else the launcher's own
/// logo in <see cref="Foreground"/> (dark on a light tab, light on a dark one), else the first letter of its name on a
/// coloured tile. Picture and logo keep their shape inside the box; free room stays empty, unless a <see cref="Plate"/>
/// gives them a ground (over a cover).
/// </summary>
public sealed class LauncherLogo : Control
{
    /// <summary>The box a launcher's own logo is drawn in (<see cref="Arcadia.Launchers.Abstractions.LauncherInfo.Logo"/>).</summary>
    private const double LogoBox = 24;

    public static readonly StyledProperty<LauncherBadge?> BadgeProperty =
        AvaloniaProperty.Register<LauncherLogo, LauncherBadge?>(nameof(Badge));

    /// <summary>The logo's colour; inherited like a text's, so a tab's text colour reaches it.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LauncherLogo>();

    /// <summary>The colour of the letter on the tile; set by the theme.</summary>
    public static readonly StyledProperty<IBrush?> MonogramBrushProperty =
        AvaloniaProperty.Register<LauncherLogo, IBrush?>(nameof(MonogramBrush));

    /// <summary>A rounded ground under picture and logo, which then stand a little inside it; set by the theme.</summary>
    public static readonly StyledProperty<IBrush?> PlateProperty =
        AvaloniaProperty.Register<LauncherLogo, IBrush?>(nameof(Plate));

    static LauncherLogo()
    {
        AffectsRender<LauncherLogo>(BadgeProperty, ForegroundProperty, MonogramBrushProperty, PlateProperty);
    }

    public LauncherBadge? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IBrush? MonogramBrush
    {
        get => GetValue(MonogramBrushProperty);
        set => SetValue(MonogramBrushProperty, value);
    }

    public IBrush? Plate
    {
        get => GetValue(PlateProperty);
        set => SetValue(PlateProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (Badge is not { } badge || size <= 0)
        {
            return;
        }

        var box = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);
        if (Plate is { } plate && (badge.Picture is not null || badge.Shape is not null))
        {
            context.DrawRectangle(plate, null, box, size / 4, size / 4);
            box = box.Deflate(size * 0.18);
            size = box.Width;
        }

        if (badge.Picture is { } picture)
        {
            var (width, height) = (picture.Size.Width, picture.Size.Height);
            var scale = Math.Min(size / width, size / height);
            var target = new Rect(box.Center.X - (width * scale / 2), box.Center.Y - (height * scale / 2), width * scale, height * scale);
            context.DrawImage(picture, new Rect(picture.Size), target);
        }
        else if (badge.Shape is { } shape)
        {
            var scale = size / LogoBox;
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(box.X, box.Y)))
            {
                context.DrawGeometry(Foreground, null, shape);
            }
        }
        else
        {
            context.DrawRectangle(badge.TileBrush, null, box, size / 4, size / 4);
            var text = new FormattedText(
                badge.Monogram,
                Formatting.Culture,
                FlowDirection.LeftToRight,
                new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, FontWeight.ExtraBold),
                size * 0.5,
                MonogramBrush);
            context.DrawText(text, new Point(box.Center.X - (text.Width / 2), box.Center.Y - (text.Height / 2)));
        }
    }
}
