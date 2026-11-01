# Launchers (`src/Arcadia.Launchers`)

Steam and Epic scanners, VDF parsers, account sign-in, install monitors, process watch, `GameLaunchService`. Read in full.

**Assessment.** Good shape for code that parses other programs' files: shared-read opens, errors as values in the
account code, launcher URIs built from validated integers or escaped segments, `installdir` confined to
`steamapps/common|music`, `appinfo.vdf` bounded in size/depth/index, DPAPI store never logs tokens, every HTTP call
has a 10 s timeout. The two text/binary VDF parsers that read attacker-writable files are the exception: unbounded
recursion, and a stack overflow is the one exception .NET cannot catch.

| ID | Sev | Cat | Finding |
|---|---|---|---|
| LNCH-1 | High | Robustness/Security | `TextVdf` recursion unbounded → stack overflow ends Arcadia |
| LNCH-2 | High | Robustness/Security | `BinaryVdf` recursion unbounded → same, from `shortcuts.vdf` |
| LNCH-3 | Medium | Performance | Steam install monitor re-parses every manifest every 2 s |
| LNCH-4 | Medium | Robustness | Sign-in leaks a disposed CTS after an unexpected error; B then throws |
| LNCH-5 | Medium | Architecture | Built-in launchers depend on `Arcadia.Core`, not only Abstractions |
| LNCH-6 | Medium | Performance/Robustness | Epic progress walks the whole install tree each poll; location unchecked |
| LNCH-7 | Low | Security | `DirectLauncherClient` sink runs any `ProtocolUri`/AUMID unchecked |
| LNCH-8 | Low | Robustness | Epic log reader records `Length` after `ReadToEnd` → lines skipped |
| LNCH-9 | Low | Robustness | Directory enumerations outside the caught set in Steam/Epic scanners |
| LNCH-10 | Low | Robustness | `SteamToken.ExpiresAt` throws on out-of-range `exp` |
| LNCH-11 | Low | Security | Web API JSON answers have no size cap |
| LNCH-12 | Low | Robustness | `libraryfolders.vdf` paths used unchecked (UNC, unplugged drives) |
| LNCH-13 | Low | Robustness | `SteamShortcuts.Add` can write an unparsable entry; `.bak` overwritten per save |
| LNCH-14 | Low | CodeQuality/Security | `Game.ExecutablePath` built from manifest content, never used |
| LNCH-15 | Low | Tests | No malformed-input or depth tests for either VDF parser |
| LNCH-16 | Low | Docs | Architecture doc names `Protobuf` and "QR sign-in", which don't exist |
| LNCH-17 | Info | CodeQuality | `EpicLastPlayedStore.CreateDefault` hard-codes `%APPDATA%\Arcadia` |

**LNCH-1** `Steam/TextVdf.cs:26-60` (`Reader.ReadMap` recurses on `TokenKind.Open` with no depth counter; verified
by the lead reviewer, the agent's line numbers were off). Contrast `SteamAppInfo.cs:199-213` (`MaxDepth = 64`,
comment "stops a crafted file from overflowing the stack"). Scenario: `libraryfolders.vdf`, any `appmanifest_*.acf`,
`localconfig.vdf`, `loginusers.vdf` are files "any local program can write" (`SteamScanner.cs:377`). ~50 KB of
`"a" {` nested exhausts the thread stack; `StackOverflowException` bypasses every `catch` incl. `LibraryService`.
`SteamInstallMonitor.Poll` re-parses manifests every 2 s (LNCH-3), so one such file is a crash loop from every
start. Attacker model: a local program with the user's rights, so this is denial of service, not escalation.
Fix: depth counter (64) + `FormatException`; test with 10,000 nested blocks. Confidence High.

**LNCH-2** `Steam/BinaryVdf.cs:76-103` (`ReadMap` → `TypeMap => ReadMap(...)`). A `shortcuts.vdf` of zero bytes
nests one level per 2 bytes. Read for every userdata folder by `SisrInstallation.cs:302`. Same fix; also throw on a
truncated nested map instead of returning silently. Confidence High.

**LNCH-3** `Steam/SteamInstallMonitor.cs:42-46,244-265`: `Poll()` → `ReadManifests` → `TextVdf.Parse` of every
`appmanifest_*.acf`, every 2 s (`App.axaml.cs:33`), no mtime/length cache (the scanner's `AppTypeCache` has one).
1,000 installed apps = ~1,000 opens and parses per 2 s beside a running game. Fix: cache by `(path, length,
lastWriteUtc)`, or skip when `content_log.txt` and the `steamapps` write times are unchanged. Not measured; Medium.

**LNCH-4** `Steam/Account/SteamOnlineAccount.cs:117-143,318-377,581-595`: `_signInRun = run` is cleared only on
normal exits; `Run`'s catch leaves it set while `using (run)` disposed it and `_state` stays `SigningIn`. Next
`CancelSignIn()` (B on the launcher page, `LauncherSettingsViewModel.cs:354,780`) → `ObjectDisposedException` → toast,
page stuck until restart. Fix: `finally` clears `_signInRun` if it equals `run`, resets the step; test where
`IWebPages.ShowAsync` throws. High on code path.

**LNCH-5** `Arcadia.Launchers.csproj:4` references Core; `using Arcadia.Core.*` in 13 files (`SteamScanner.cs:63`
`KnownLaunchers.Steam`, `EpicScanner.cs:133,261`, `SteamAccountStore.cs:36` `LogLocation.DataFolder`,
`DemoScanner.cs:415` special-cases Epic's id). CLAUDE.md DoD: built-ins use only Abstractions, as a plugin would.
Fix: launcher ids/`LauncherInfo` built by the integration (ids in Abstractions), data folder passed in by the caller;
or record the exception in `docs/architecture`. See also CORE-8, APP-9, XR-3.

**LNCH-6** `Epic/EpicInstallMonitor.cs:115,389-411,308-348`: `FractionOf` enumerates the install folder recursively
every 2 s during a download; `InstallLocation` from `Manifests\Pending\*.item` is used as-is (a stale item with
`C:\` enumerates the drive forever, `IgnoreInaccessible` keeps it from throwing). Fix: size only `.egstore`, throttle
to 10 s, reject roots/UNC/depth < 2. Medium.

**LNCH-7** `DirectLauncherClient.cs:105-125`: `Process.Start(new ProcessStartInfo(uri.Uri) { UseShellExecute = true })`
and `explorer.exe shell:appsFolder\{AppUserModelId}` (string, not `ArgumentList`). Every current producer is safe, but
the DoD rule is enforced per producer, not at the one sink. Fix: require an absolute URI with an alphanumeric
non-`file` scheme; AUMID `^[A-Za-z0-9._!-]+$`. High, defence in depth.

**LNCH-8** `Epic/EpicInstallMonitor.cs:237-238`: `_logOffset = stream.Length` after `ReadToEnd`; bytes appended in
between are never read. `SteamContentLog.cs:355` does it right (`Position`, comment "not Length"). A lost
`HandleTaskComplete` leaves an install shown as running.

**LNCH-9** `Steam/SteamInstallation.cs:52,98` (`Directory.GetDirectories(userData)`), `Epic/EpicScanner.cs:156`
(`EnumerateFiles(..., "*.item")`) have no IO guard; one transient error empties the whole launcher's library for that
scan instead of one part.

**LNCH-10** `Steam/Account/SteamToken.cs:279-282`: `DateTimeOffset.FromUnixTimeSeconds` without range check (the
scanner's `ParseUnixTime` clamps). Out-of-range `exp` → `ArgumentOutOfRangeException` → toast on every fetch.

**LNCH-11** `Steam/Account/SteamWebApi.cs:220-221`: `JsonDocument.ParseAsync` on the raw stream; only the 10 s
timeout bounds memory. `GetAvatarAsync` has `MaxAvatarBytes`; the JSON calls should have a cap (8 MB).

**LNCH-12** `Steam/SteamScanner.cs:211-234`: library paths only normalised and deduped; a library on an unplugged
network share makes `Directory.Exists` block for the SMB timeout on every 2 s poll (LNCH-3).

**LNCH-13** `Steam/SteamShortcuts.cs:414-420,487-498,529`: `Quote` and `WriteString` don't reject `\0`/`"`; `.bak`
is written before every save, so the second save replaces the only good backup. Callers pass constants today.

**LNCH-14** `Epic/EpicScanner.cs:267-269`: `Path.Combine(installLocation, launchExecutable)` returns an absolute
`LaunchExecutable` verbatim; no consumer exists (`rg ExecutablePath src`). Drop or validate now, before a "start
directly" feature hands it to `Process.Start`.

**LNCH-15** `tests/Arcadia.Core.Tests/TextVdfTests.cs`, `SteamShortcutsTests.cs`: malformed cases cover unbalanced
braces only; no deep nesting, truncation, unknown type byte. Add with LNCH-1/2.

**LNCH-16** `docs/architecture/README.md:10`: `Protobuf` has no source file; the sign-in is Steam's store page in
the web view (`account.md:9-11`). **LNCH-17** `Epic/EpicLastPlayedStore.cs:310-311` vs `SteamAccountStore` using
`LogLocation.DataFolder`.

**Done well.** `SteamAppInfo.Reader` as a model bounded parser; `InstallDirectoryOf` and Epic's `AppPath` escaping
with the reasoning in comments; account layer (HTTPS constants, `x-eresult` mapping, avatar magic bytes, DPAPI, test
that the log holds no token); every log tailer opens with `FileShare.ReadWrite | Delete` and handles rotation;
`GameLaunchService.TryStartAsync` really never throws and is tested per branch; `ProcessWatch.PathOf` via
`QueryFullProcessImageName` with a measured rationale; atomic `.tmp` + `Move` writes in three stores.
