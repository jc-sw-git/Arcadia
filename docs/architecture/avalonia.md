# Avalonia 12: rules that are easy to get wrong

Read before changing `.axaml` files, styles or `Theme/Theme.axaml` (pointer in `CLAUDE.md`).

- A locally set property (e.g. `Fill="…"`, `Stroke="…"`) beats any style — state colours must
  come entirely from styles (`Classes.x="{Binding …}"` + `Style Selector="….x"`).
- `RenderTransform` can't be animated with key frames → give the element its own
  `<RotateTransform/>` and animate `RotateTransform.Angle` (see `Path.spinner`).
- `SystemDecorations` is now `WindowDecorations`. Box shadows of `Button.plain` go on the
  template part: `Button.x /template/ Border#Chrome`.
- Fluent's `Button` and `ItemsControl` clip to their bounds, so an outer focus ring (BoxShadow) is cut off;
  `Theme.axaml` sets `ClipToBounds=False` on both. A `UserControl` clips too; set it on the one whose edge a ring reaches. A focus ring also needs room in clipping parents
  (`ScrollViewer` padding). With several BoxShadows, the later one paints on top.
- Compiled bindings: every `DataTemplate` needs `x:DataType`; reach the window's view model
  from inside a template with `$parent[Window].((vm:MainViewModel)DataContext)?.Prop`.
- A binding path through a property that can be null (`Detail.X`, `FocusedGame.X`, a `DataContext` not set yet)
  logs a binding error each time, `FallbackValue` or not. Write `Detail?.X` and give a value type a
  `TargetNullValue` (`IsVisible="{Binding Detail?.IsFromLibrary, TargetNullValue=False}"`). `XamlBindingTests` checks
  both on every `.axaml` against the App's properties declared nullable (`T? Name`), so declare nullable what can be null.

