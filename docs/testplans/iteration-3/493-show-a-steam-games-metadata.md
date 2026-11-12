# #493 Show a Steam game's metadata on the detail page

Derived from the specification in #43 (decisions, scenarios 1, 3, 8, 9, 10, 12 and the failure cases that apply) and
the scope of #493. Automated cases use made-up Steam files (`appinfo.vdf`, tag list) and Epic catalogs with made-up
AppIDs, names and texts, and a fake Steam store that records every request; nothing reaches the real store. Under
#517 there are no window tests per feature: cases marked `E2E: no (window)` are checked in the quality engineer's run.
"The info line" is the line under the title (genres · developer · year, age rating badge), "Infos" the detail page
area with the full description and every field with its source. The owner's cases are collected by the closing
thread of #43 and handed over there, not in this PR.

Open points for the implementation, as the cases assume them (named in the PR; changed only with the owner):
A1 "Infos" does not exist on `main` yet; #493 adds it to the rail as in the artboard. A2 a long description in
"Infos" is read with ▲ ▼ (and the right stick), B goes back to the rail. A3 the names of Steam's store tags come from
a German tag list that Arcadia keeps locally (no request beside `appdetails`); where it comes from is decided in the
PR. A4 while "Alle Metadaten neu laden" runs, the old data stays visible until the new one is there. A5 with no game
missing, the progress row reads "Alle Metadaten geladen" (greyed); the wording goes into the PR's UI texts.

**Status:** Automated: T493-1 to -31 · QE: T493-32 to -36 · Open for the owner: T493-37, T493-38, T493-39, T493-40

**Steam data from `appinfo.vdf` and the store page**

- [ ] **T493-1** Given a Steam game with genres, developer, publisher, release date and store tags in `appinfo.vdf`,
      when Arcadia scans, then the detail page shows genres, developer and year in the info line at once from the
      local file (before any store answer), without a key (#43 scenario 1). · E2E: yes
- [ ] **T493-2** Given the same game and a store answer with a German description and `ratings.usk`, then shortly
      after the scan the short description in "Übersicht", the full one in "Infos" and the USK badge in the info line
      appear without reopening the page; with the page open they appear while it is shown. · E2E: yes
- [ ] **T493-3** `appinfo.vdf` parsing: genres, primary genre, store tag ids, developer/publisher from `associations`
      and from `extended/developer|publisher` (either one present), release date; a field missing or empty is left
      out, a broken or truncated entry costs only that game's data, never the scan. · E2E: no (unit test)
- [ ] **T493-4** Store tags: ids are turned into names through the tag list (A3); an id not in the list is dropped;
      the tags line shows the first tags in Steam's order and "+n" for the rest. · E2E: no (unit test)
- [ ] **T493-5** Store page parsing (`appdetails`, `l=german`): description, short description, genres, ratings
      `usk`, `pegi`, `esrb`, `steam_germany`; `success: false`, missing `data`, broken JSON, wrong types, an answer
      for another AppID or one over the size limit count as no answer for that game. HTML in descriptions is reduced
      to text (no tags, entities decoded, no images, links or scripts reach the page). · E2E: no (unit test)

**Genres, language, age rating, merge**

- [ ] **T493-6** Genre mapping: each provider's genres map onto the fixed German list (Action, Abenteuer,
      Rollenspiel, Strategie, Simulation, Rennspiel, Sport, Shooter, Puzzle, Plattformer, …); Steam's English
      (`appinfo.vdf`) and German (store) names map to the same entry, duplicates are shown once, unmapped ones are
      dropped; a game with only unmapped genres shows none. · E2E: no (unit test)
- [ ] **T493-7** Language rule: a description that is not German, or contains `?` inside a word ("K?ln"), counts
      only when no provider has a German one; a game that exists only in English shows its English description.
      Cases: German only, English only, German broken plus English, both German, empty or whitespace. · E2E: no (unit test)
- [ ] **T493-8** Age rating: a real `usk` is shown as USK with source "Steam"; without one, `steam_germany` counts as
      USK and "Infos" names the source "laut Steam"; PEGI and ESRB as delivered; none at all: no badge, no empty
      place. Unknown rating values are dropped, never shown raw. · E2E: no (unit test)
- [ ] **T493-9** Merge per field in provider order: a field missing from the first provider comes from the next;
      each field keeps the provider it came from; a provider that delivers nothing changes nothing. Checked with
      fake providers that declare different fields. · E2E: no (unit test)
- [ ] **T493-10** Given an Epic game, then only its developer is shown (info line and "Infos" with source "Epic");
      description, genres, year, age rating and tags are missing, no error message, no placeholder text
      (#43 scenario 3). The catalog's "description" and `dateAdded` are never used. · E2E: yes
- [ ] **T493-11** A game without any metadata (a demo or plugin game, or nothing loaded yet): no info line,
      no description, no tags line, "Infos" says there is nothing yet; the layout of title and buttons stays as
      before. · E2E: no (view model test)

**Detail page** (look: window, checked in the quality engineer's run)

- [ ] **T493-12** Info line under the title: "Action, Abenteuer · <Developer> · 2021" and the USK badge; a missing
      part leaves no stray "·"; long genre lists and developer names end with "…" in one line. Colours and the badge
      from `Theme.axaml`. · E2E: no (window)
- [ ] **T493-13** "Übersicht": the short description (at most the lines the artboard gives, then "…") and the tags
      line with "+n"; the facts sit where #499/#500 place them. · E2E: no (window)
- [ ] **T493-14** "Infos": full description, then genres, developer, publisher, release date (German date), tags and
      every age rating, each with its source ("Steam", "Epic", "laut Steam"); the source shows only here. Controller
      alone: ▲ ▼ scroll a long description (A2), B goes back to the rail, B again closes the page. · E2E: yes

**Fetching, throttling, cache**

- [ ] **T493-15** Every store request goes over HTTPS to the store host only, carries only the AppID and `l=german`
      (no cookies, no Steam sign-in, no account, path or name); an AppID that isn't a plain number is never put into a
      request. · E2E: no (unit test with the recording fake)
- [ ] **T493-16** Throttling: at most Steam's limit (about 200 requests per 5 minutes) is sent; HTTP 429 (with and
      without `Retry-After`) waits and continues with the same game, no toast (fake time). · E2E: no (unit test with fake time)
- [ ] **T493-17** No network, no answer within 10 s, 5xx: the game is skipped silently, no toast, one log line per
      run, not per game; it is asked again at the next scan. · E2E: no (unit test with fake time)
- [ ] **T493-18** After every scan only games without store data are fetched, in the background; a scan while a fetch
      runs asks no game twice; a game already cached is never asked again by a scan. · E2E: no (view model test)
- [ ] **T493-19** Raw data is cached per provider under `%LOCALAPPDATA%\Arcadia\metadata`, written to a temporary
      file and renamed; a damaged or unreadable cache file is discarded with one log line and fetched again; values
      from it are checked like a store answer (T493-5), nothing from it reaches a path or URL unchecked. · E2E: no (unit test)
- [ ] **T493-20** Offline after a fetch: start Arcadia without network, everything loaded once is shown as before, no
      message (#43 scenario 10). · E2E: yes (acceptance test across a restart)
- [ ] **T493-21** Right after the start, cached metadata is shown at once and nothing is fetched before the first scan
      has finished. · E2E: no (view model test)
- [ ] **T493-22** Arcadia closed during a fetch: it closes without waiting for a request; at the next start the cache
      is intact and the fetch continues with the games still missing. · E2E: no (unit test)
- [ ] **T493-23** A game leaving the library and coming back shows its cached data without a new request.
      · E2E: no (unit test)

**Settings "Datenquellen"**

- [ ] **T493-24** The metadata rows: the Steam row's info text names the limit (about 200 requests per 5 minutes
      without a key) and what Steam gets (only the AppID); the progress row "Fehlende Metadaten: 412 von 1.000, noch
      etwa 20 Minuten" with German number format, "noch etwa 1 Minute", "weniger als eine Minute"; none missing as A5.
      Layout at 1920 × 1080, colours from `Theme.axaml`. · E2E: no (window)
- [ ] **T493-25** Progress row counts: the numbers follow the run (fake store, fake time), a skipped game (T493-17)
      stays missing, the time estimate follows the limit and the observed rate; no toast during or after the run
      (#43 scenario 9). · E2E: no (view model test)
- [ ] **T493-26** "Alle Metadaten neu laden": A asks first; B or "Abbrechen" changes nothing and sends nothing; yes
      refetches every game, the old data stays visible until replaced (A4), own titles and tags (from #495/#496,
      when there) are untouched (#43 scenario 8). · E2E: yes
- [ ] **T493-27** "Alle Metadaten neu laden" failing as a whole (no network at all): one status message; a background
      run failing as a whole: none. A second A while a run goes starts no second run. · E2E: no (view model test)
- [ ] **T493-28** Controller only, B back: Settings → "Datenquellen" → the metadata rows → "Alle Metadaten neu laden"
      A → question → A → B; detail page → "Infos" → ▲ ▼ → B → B; no mouse or keyboard needed (#43 scenario 12).
      · E2E: yes

**Robust, secure, fast**

- [ ] **T493-29** No exception from parsing, the cache or the network ends Arcadia; unexpected ones land in
      `arcadia.log` with stack trace and as a toast; descriptions, account data and request URLs with more than the
      AppID never reach the log. · E2E: no (unit test over the log sink)
- [ ] **T493-30** 1,000 Steam games without store data, each answer taking 2 s: the library is usable at once, a
      focus move stays under 50 ms, the settings page and the progress row stay operable while the fetch runs.
      · E2E: yes (speed test like T489-24)
- [ ] **T493-31** Architecture: the Steam provider lives in the Steam folder, the Epic provider in the Epic folder, both
      only through `Arcadia.Launchers.Abstractions`; Core and UI don't special-case a launcher (`ArchitectureTests`).
      · E2E: no (unit test)
- [ ] **T493-32** Release build, made-up data: start to usable library < 2 s with 1,000 cached games; < 400 MB RAM
      with 5,000 games with cached metadata; idle CPU < 1 % after the fetch. · E2E: no (Release timings; quality engineer's run)
- [ ] **T493-33** Look of T493-12 to -14 with `--demo` and a made-up cache: short and long texts, missing fields,
      English description, "laut Steam", at 1920 × 1080. · E2E: no (window)
- [ ] **T493-34** Look of T493-24 and the question of T493-26. · E2E: no (window)
- [ ] **T493-35** Mouse and keyboard: a click on "Infos" opens it, the mouse wheel scrolls the description, a click on
      "Alle Metadaten neu laden" asks; keys as elsewhere (WASD/arrows, Space = A, Esc = B). · E2E: no (window: real clicks)
- [ ] **T493-36** A store description with markup, images, links, very long text and emoji shows as plain text,
      readable, nothing clickable opens a browser. · E2E: no (window)

**Owner** (real Steam store, own library; collected by the closing thread of #43)

- [ ] **T493-37** **Owner:** own Steam library: after a scan, open a few detail pages (an older game, a new one, one
      only in English, one with USK 18): info line, USK badge, German description and tags match the game's Steam
      store page; no page shows another game's data. · E2E: no (real Steam store, own library)
- [ ] **T493-38** **Owner:** an Epic game shows only its developer, nothing broken, no error. · E2E: no (real Epic catalog)
- [ ] **T493-39** **Owner:** the first fetch over the own library: "Fehlende Metadaten" counts down sensibly, the time
      estimate is plausible, the library and a running game stay fluid, Steam keeps working (no sign-out, no block
      by Steam). · E2E: no (real Steam store and limit, own library)
- [ ] **T493-40** **Owner:** network off, start Arcadia: the metadata loaded before is all there, no message.
      · E2E: no (real PC offline)

Automated tests:

| Test cases | Tests |
|---|---|
| T493-1 to -31 | named by the implementation PR |

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
