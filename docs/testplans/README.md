# Testpläne

Ein Testplan pro Feature-Iteration, als Ordner (`iteration-1/`, `iteration-2/`, …): eine Datei pro
Issue (`<Issue dreistellig>-<titel>.md`, z. B. `015-einstellungsseite.md`) und ein `README.md` als
Übersicht mit der Liste der Dateien, dem Abschluss-Test, dem explorativen Test und den Abnahmetests.
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
  So bleibt die Historie vollständig, und der Abschluss-Test weiß, was das Refactoring berührt hat.
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
- **Die Häkchen** setzt nur der Abschlusstest am Ende der Iteration (Issue „Abschluss-Test“), am
  installierten Release-Kandidaten. Frühere Läufe zählen als Historie, nicht als Abnahme, weil
  spätere Issues das Verhalten noch ändern können.

## Ablauf einer Iteration

1. **Feature-Iteration N** (Meilenstein „Iteration N“): Issues umsetzen, jeder PR ergänzt den Testplan.
2. **Abschluss-Review** (Code): Prüfliste im Review-Issue, Befunde beheben oder als Issue einplanen.
3. **Abschluss-Test** (manuell): alle Testfälle am Release-Kandidaten, danach ein explorativer
   Test mit Auftrag und Zeitrahmen. Ergebnis: Häkchen, Verlauf, die Liste der Abnahmetests.
4. **Retrospektive** (eigenes Issue, erst nach Review und Abschluss-Test): Für jeden Prozess
   entscheidet der Owner, ob er bleibt, geändert wird oder wegfällt.
5. **Release.**
6. **Abnahme-Iteration N** (Meilenstein „Abnahme Iteration N“): alle Testfälle mit `E2E: ja`
   werden automatisierte Abnahmetests in der Pipeline. Feature-Iteration N+1 beginnt erst, wenn
   sie abgeschlossen ist, damit Änderungen in N+1 Brüche in Features aus N sofort zeigen.
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
