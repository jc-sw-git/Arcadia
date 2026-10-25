# Testpläne

Ein Testplan pro Feature-Iteration, als Ordner (`iteration-1/`, `iteration-2/`, …): eine Datei pro
Issue (`<Issue dreistellig>-<titel>.md`, z. B. `015-einstellungsseite.md`) und ein `README.md` als
Übersicht mit der Liste der Dateien, der Testplan-Durchsicht, dem explorativen Test und den Abnahmetests.
Er ist die einzige Quelle für alle manuellen Tests dieser Iteration: was getestet werden soll, wer
wann was getestet hat, was dabei schiefging und was daraus wurde. Wer ein Issue testet oder
ergänzt, liest nur dessen Datei, nicht den ganzen Ordner.

## Regeln

- **Jeder PR, der ein Issue schließt, ergänzt den Testplan** um eine Datei mit den Testfällen dieses Issues
  und eine Zeile in der Übersicht
  (Definition of Done in `CLAUDE.md`; der CI-Check `Testplan` prüft das). Braucht ein Issue keine
  neuen manuellen Tests (z. B. ein Refactoring von Features, die schon automatisiert oder durch
  bestehende Testfälle abgedeckt sind), bekommt es trotzdem einen Abschnitt mit einer Zeile, warum,
  und nennt die bestehenden Testfälle, die es berühren:
  `# #67 MainViewModel aufteilen` → „Keine neuen Testfälle: reines Refactoring; berührt T41-3 bis T41-6.“
  So bleibt die Historie vollständig, und die Testplan-Durchsicht weiß, was das Refactoring berührt hat.
- **Testfall-IDs** heißen `T<Issue>-<laufende Nummer>`, z. B. `T15-2`. Was zu keinem Issue
  gehört (das Grundgerüst in Iteration 1), steht in `grundgeruest.md` mit `G-<n>`. Sie ändern sich nie; ein
  überholter Testfall wird ~~durchgestrichen~~ mit Grund, nicht gelöscht.
- **Jeder Testfall ist markiert:** `E2E: ja` (soll als automatisierter Abnahmetest in die
  Pipeline) oder `E2E: nein (Grund)` (z. B. echter Steam Controller, UAC, echte Launcher-Installation).
- **Jeder Testlauf ist eine eigene Zeile** in der Tabelle „Verlauf“ des Issues: Datum, wer,
  Build und Umgebung, Ergebnis, Folge. Auch Tests während der Umsetzung (Demo-Daten, virtuelles
  Display, nur mechanisch) stehen dort, mit ehrlicher Umgebungsangabe.
- **Fehlschläge** nennen Ursache und Folge: das neue Issue und wohin es ging (in dieser Iteration
  behoben, nächste Iteration, Backlog).
- **Die Häkchen** setzt der vollständige Test des Owners vor dem Merge (siehe „Vor dem Merge“). Findet die
  Testplan-Durchsicht am Ende der Iteration, dass ein späteres Issue das Verhalten geändert hat, wird der
  Testfall wieder offen und am Release-Kandidaten erneut getestet.

## Vor dem Merge (Owner 2026-10-25)

1. **Abnahmetests zuerst:** Die Testfälle des Issues mit `E2E: ja` werden im Umsetzungs-PR als Abnahmetests
   geschrieben (Regeln unter Abnahme-Iteration), bevor der Owner testet. Geht ein Fall noch nicht automatisch
   (z. B. Headless-UI vor #191/#192), sagt der PR das pro Fall.
2. **Codex-Review** kommt wie bisher als Kommentar im PR.
3. **Manueller Test:** Der Owner führt die Testplan-Datei des Issues vollständig aus, am PR-Build, und gibt sein
   Feedback in einem Schwung.
4. **Nacharbeit:** Der Umsetzungs-Thread behebt das Feedback des Owners und die Codex-Kommentare zusammen in
   einem Durchgang. Ein neuer Thread übernimmt den PR nur, wenn der Umsetzungs-Thread schon sehr lang ist; er
   liest dann PR, Issue und Testplan-Datei statt des alten Threads.
5. Der Owner testet nur noch, was die Nacharbeit geändert hat, und merged.

## Ablauf einer Iteration

1. **Feature-Iteration N** (Meilenstein „Iteration N“): Issues umsetzen, jeder PR ergänzt den Testplan.
2. **Abschluss-Review** (Code): Prüfliste im Review-Issue, Befunde beheben oder als Issue einplanen.
   Die Prüfliste fragt auch, ob `CLAUDE.md`, `docs/` und Skills noch stimmen.
3. **Testplan-Durchsicht** (Claude): alle Testplan-Dateien der Iteration gegen den Stand von `main`. Pro Datei:
   Deckt sie noch ab, was spätere PRs geändert haben (Lücke), und beschreibt sie noch das aktuelle Verhalten
   (Drift)? Ergebnis: Testfälle ergänzen oder anpassen und die Liste, was am Release-Kandidaten erneut manuell
   getestet werden muss, mit Grund; „nichts“ ist ein gültiges Ergebnis. Am Release-Kandidaten testet der Owner
   diese Liste, dann einen explorativen Test (Auftrag und Zeitrahmen stehen vorher in der Übersicht).
   - **Messung:** die Budgets aus `CLAUDE.md` einmal am PC des Owners (Release-Build), als Zeile im Verlauf.
   - **Update-Fälle** testet ein zweiter Release-Kandidat (z. B. rc.1 → rc.2 per Self-Update),
     nicht erst das nächste Release.
   - **Funde:** Vor dem Release wird nur behoben, was Daten, Sicherheit oder eine Kernfunktion bricht;
     alles andere wird ein Issue für Iteration N+1. Neue Ideen aus dem Test werden nur als Issue angelegt.
4. **Retrospektive** (eigenes Issue, erst nach Review und Testplan-Durchsicht): Für jeden Prozess
   entscheidet der Owner, ob er bleibt, geändert wird oder wegfällt.
5. **Release.**
6. **Abnahme-Iteration N** (Meilenstein „Abnahme Iteration N“): die Kernabläufe (Start, Navigation,
   Suche, Spielstart, Einstellungen und was N daran ändert) werden automatisierte Abnahmetests in der
   Pipeline; Abnahme-Iteration 1 baut dafür zuerst die Infrastruktur. Die übrigen Testfälle mit
   `E2E: ja` werden je ein Issue und laufen in Iteration N+1 mit (Retro Iteration 1, Owner 2026-10-24).
   Feature-Iteration N+1 beginnt erst, wenn die Abnahme-Iteration abgeschlossen ist, damit Änderungen
   in N+1 Brüche in Features aus N sofort zeigen.
   Regeln für die Abnahmetests (Owner 2026-10-24, #184):
   - Sie liegen in `tests/Arcadia.App.Tests` und sprechen nur über `ArcadiaDriver` (Tasten, Zeit, Neustart)
     und was die View Models zeigen. Steam und Epic lesen dort nachgestellte Dateien mit erfundenen IDs
     (`Fixtures/`, `ArcadiaDriver.StartWithLaunchersAsync`, #193); nichts öffnet einen echten Launcher. Jeder Test trägt die IDs seiner Testfälle als `[Trait("Testfall", "T15-1")]`;
     die Übersicht der Iteration listet sie unter „Abnahmetests“.
   - **Einmal rot:** Jeder neue Abnahmetest ist vor dem PR einmal fehlgeschlagen, weil das Feature gezielt kaputt
     gemacht wurde. Der PR nennt pro Test die Änderung, mit der er rot wurde.
   - **Codex-Review** je PR: deckt jeder Test den Text seiner Testfall-IDs ab, wo sind Lücken?
   - **Mutationstests** (Stryker.NET) einmal am Ende jeder Feature-Iteration; überlebende Mutanten werden Tests
     oder `type:tech-debt`-Issues.
7. **Feature-Iteration N+1** beginnt mit den großen `type:tech-debt`-Befunden aus dem Review,
   jetzt abgesichert durch die Abnahmetests.

## Vorlage für ein Issue

Datei `iteration-<n>/<Issue dreistellig>-<titel>.md`:

```markdown
# #<Issue> <Titel>

- [ ] **T<Issue>-1** <Was tun, was muss passieren; nur Controller, B geht zurück> · E2E: ja
- [ ] **T<Issue>-2** <…> · E2E: nein (<Grund>)

Verlauf:

| Test | Datum | Wer | Build / Umgebung | Ergebnis | Folge |
|---|---|---|---|---|---|
| T<Issue>-1 | JJJJ-MM-TT | Claude | PR #…, Demo-Daten | ✅ | – |
| T<Issue>-2 | JJJJ-MM-TT | Juri | v0.x.y-rc1, echter PC | ❌ <Ursache> | #… (in dieser Iteration behoben / Iteration N+1 / Backlog) |
```
