# UI texts

Every text the player reads lives in a resource file, so a text change or a later translation has one place. Wording
rules: `docs/product/ui-texts.md`.

## Where texts live

| File | Class | Texts of |
|---|---|---|
| `src/Arcadia.App/AppTexts.resx` | `Arcadia.App.AppTexts` | views, view models and services of the App |
| `src/Arcadia.ControllerSupport/ControllerSupportTexts.resx` | `Arcadia.ControllerSupport.ControllerSupportTexts` | controller emulation, Pan's client, driver installation |
| `src/Arcadia.Core/CoreTexts.resx` | `Arcadia.Core.CoreTexts` | launcher names and keywords, logo checks |
| `src/Arcadia.Input/InputTexts.resx` | `Arcadia.Input.InputTexts` | controller names |
| `src/Arcadia.Launchers/<Launcher>/<Launcher>Texts.resx` | `Arcadia.Launchers.<Launcher>.<Launcher>Texts` | one launcher's texts, kept in its folder so a plugin brings its own |

Not in resources: log lines, exception messages that only reach the log (marked `// not a UI text (…)` on their line),
brand and product names, key names such as `Esc`. An exception message the player can read (a toast's detail, the
install dialog, a reason on the controller page) is a UI text and comes from a resource. Pan and `Arcadia.Pan.Core`
keep their few texts in code: Pan runs as a Windows service without Arcadia's assemblies, and its service
description is translated by Windows' own means (`ServiceDescriptions`).

## How it works

- A project with texts sets `<HasUiTexts>true</HasUiTexts>`; `Directory.Build.targets` then embeds its `*Texts.resx`
  as `<AssemblyName>.<FileName>` and runs `src/Arcadia.Texts.Generator`, a Roslyn source generator, so the build
  needs nothing but `dotnet build` on any platform.
- The generator makes a static class per file: a property per text without placeholders (`AppTexts.Cancel`), a
  method per text with placeholders whose parameters are the placeholders in name order
  (`AppTexts.GameStarting(game)`). Two or more values are passed by name (`AppTexts.GameOpening(game: title,
  launcher: name)`), so a rewording that moves the placeholders can't swap them; `UiTextsStayInResourcesTests` checks it.
- Both read through `UiText` in `Arcadia.Launchers.Abstractions` (the plugin contract), the one place texts are
  filled. It never throws: a placeholder without a value stays visible (`{game}`), a format the value doesn't know
  is left out, a missing text shows its key; each
  is logged once per run to `arcadia.log` (`UI-Text …`), with the key and the placeholder's name, never the values,
  and no toast.
- Placeholders are English names in braces, with an optional .NET format: `{count}`, `{seconds:0}`, `{time:HH:mm}`.
  Numbers and dates are formatted with the current culture, as string interpolation does. A value is inserted as it
  is, so a game called `{launcher} Edition` stays that.
- The neutral resources are German; there is no satellite assembly yet, so every UI culture gets German.
- A test can swap a class's `Resources` for a `ResourceManager` of its own, e.g. one with a broken text.

## Adding or changing a text

1. Add a `<data name="Key" xml:space="preserve"><value>…</value></data>` to the project's `*Texts.resx`. The key
   is an English C# identifier saying what the text is (`SearchNoHit`, `ErrorContextLoadLibrary`).
2. Use it: `AppTexts.Key` or `AppTexts.Key(game: title)` in code, `{x:Static app:AppTexts.Key}` in a view
   (`xmlns:app="using:Arcadia.App"`).
3. Plural or conditional texts are separate keys chosen in code (`GameCountOne` / `GameCountMany(count)`), whole
   sentences where possible, so a translation can change word order.

`UiTextsStayInResourcesTests` fails on a German text left in code or written into a view (log calls exempt, and any
line marked `// not a UI text`); `UiTextTests` covers the fallback.
