# Avalonia 12: rules that are easy to get wrong

Read before changing `.axaml` files, styles or `Theme/Theme.axaml` (pointer in `CLAUDE.md`).

- A locally set property (e.g. `Fill="…"`, `Stroke="…"`) beats any style — state colours must
  come entirely from styles (`Classes.x="{Binding …}"` + `Style Selector="….x"`).
- `RenderTransform` can't be animated with key frames → give the element its own
  `<RotateTransform/>` and animate `RotateTransform.Angle` (`ctl:LoadingSpinner` brings it).
- A style animation inside a hidden panel can keep running (`PlaybackBehavior` doesn't stop it), and its clock redraws the
  window every frame: CPU and garbage at idle. An endless one goes on a class set only while the element
  `IsEffectivelyVisible` and its window is active (`ctl:LoadingSpinner` and `Path.spinner.turning`): a turning spinner
  costs about a fifth of a core, and in the background no trim gives its garbage back (#680).
- `SystemDecorations` is now `WindowDecorations`. Box shadows of `Button.plain` go on the
  template part: `Button.x /template/ ctl|ButtonChrome#Chrome`.
- Fluent's `Button` and `ItemsControl` clip to their bounds, so an outer focus ring (BoxShadow) is cut off;
  `Theme.axaml` sets `ClipToBounds=False` on both. A `UserControl` clips too; set it on the one whose edge a ring reaches. A focus ring also needs room in clipping parents
  (`ScrollViewer` padding). With several BoxShadows, the later one paints on top.
- Compiled bindings: every `DataTemplate` needs `x:DataType`; reach the window's view model
  from inside a template with `$parent[Window].((vm:MainViewModel)DataContext)?.Prop`.
- A binding path through a property that can be null (`Detail.X`, `FocusedGame.X`, a `DataContext` not set yet)
  logs a binding error each time, `FallbackValue` or not. Write `Detail?.X` and give a value type a
  `TargetNullValue` (`IsVisible="{Binding Detail?.IsFromLibrary, TargetNullValue=False}"`). `XamlBindingTests` checks
  both on every `.axaml` against the App's properties declared nullable (`T? Name`), so declare nullable what can be null.

- Fonts are embedded (`ArcadiaFonts`, used by `Program` and the tests): Figtree for all text, Inter only as fallback
  for symbols Figtree lacks (▲▼◀▶ ↵ ⇧ ⌫ ␣). `Theme.axaml` sets Fluent's `ContentControlThemeFontFamily` to the same
  pair. A character neither font has is drawn as a theme icon (⧉ `PadIconView`, ≡ `PadIconOptions`, ↹ `KeyIconTab`);
  `EmbeddedFontTests` fails on any other in the App's strings and views. A Fluent `Button` centres itself vertically:
  set `VerticalAlignment` when it must fill a cell.
- A `ScrollBar` sets its own `IsVisible` whenever it has something to scroll, overriding a binding: show or hide it
  through a panel around it.
- Memory (#636): Avalonia keeps a record (about 400 bytes) for every style whose selector might match an element: a
  `TextBlock.x` style costs one on every `TextBlock`, whether it has class `x` or not. A style of one view goes into that
  view's group in `Theme.axaml` (`<Style Selector="views|GameDetailView">` with `^ TextBlock.x` inside); it then costs
  only inside that view. Elements built many times over (the keyboards' keys, every button's chrome) are types of their
  own (`ctl:KeyLabel`, `ctl:ButtonChrome`), so the window's `TextBlock`/`Border` styles skip them. `StyleCostTests`
  (window guard) bounds the records per element.
- Start (#637): the window and every page, dialog and keyboard in it are built before the first frame, so what they do at
  construction delays the library. A control that builds its children from properties (`KeyHint`, `PadGlyph`) builds
  them in `MeasureOverride`, not at each property change: hidden pages aren't measured, so they cost nothing until shown.
  The on-screen keyboards are built after the first frame (`MainWindow.BuildKeyboardsOnceLibraryShows`); a shut drawer
  kept in the layout waits for it with class `afterFirstFrame`. Bindings in the library's cover template that never change
  for a tile are `OneTime`. The release is published ReadyToRun. `arcadia.log`'s line `Start: …` gives each step in ms
  since the process started, up to `Bibliothek im Bild`.
- Memory (#685): every element built costs about 15 KB of style records, whether it is shown or not. What is built many
  times over is built only where it is seen: a page's covers through `ctl:BuiltInSight` (in sight during layout, two rows
  ahead after the frame so a focus move stays fast; covers far from sight go only while the player is quiet, through the
  memory trim, and the first rows a page turn shows always stay), and a part that is shown rarely, such as a cover's
  update, running or install badge, through `ctl:BuiltWhenShown` (built at its first measure; hidden elements aren't
  measured). Its `IsVisible` binding goes on the wrapper; the template keeps the rest.
