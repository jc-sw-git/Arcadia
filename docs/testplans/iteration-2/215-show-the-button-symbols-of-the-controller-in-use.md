# #215 Show the button symbols of the controller in use

Controllers at hand: 8BitDo Ultimate 2 (Xbox set), DualSense (PS5), Switch Pro (Nintendo), Steam Controller (owner);
DualShock 4 (PS4) only automated. Sets: Xbox A B X Y, LB RB, LT RT, ⧉ ≡; PlayStation ✕ ○ □ △, L1 R1, L2 R2, PS5
Create/Options icons, PS4 "Share"/"Options"; Nintendo B A Y X (bottom button = B), L R, ZL ZR, LS RS for the stick
clicks, − +; Steam Controller A B X Y, L1 R1, L2 R2, L3 R3, ⧉ ≡. One controller at a time unless the case says two.

- [ ] **T215-1** With each of the four controllers alone: the library's footer, the detail page (A held), the settings
      (≡) and the search's on-screen keyboard (X) show that controller's symbols, also inside texts (e.g. the mouse mode
      help in the settings). · E2E: yes
- [ ] **T215-2** 8BitDo and DualSense connected: a press on the DualSense switches every hint on screen to PlayStation
      at once, a press on the 8BitDo back to Xbox. · E2E: yes
- [ ] **T215-3** Same two: the idle controller lying on the table (sticks untouched, a little off centre) switches
      nothing; pushing its stick fully does. · E2E: yes
- [ ] **T215-4** Arcadia started with the DualSense connected: PlayStation symbols before any press. Started with no
      controller: the keyboard's keys (Leertaste, Esc, …). Nothing is remembered from the last run. · E2E: yes
- [ ] **T215-5** 8BitDo and DualSense connected, DualSense in use: unplug it, the 8BitDo's symbols show; unplug that
      too, the keyboard's keys show. Plug the DualSense in again: nothing changes until it is pressed. · E2E: yes
- [ ] **T215-6** Switch Pro: the hint "Spielen" shows **B**, and the bottom button (B on the pad) starts the game. · E2E: yes
- [ ] **T215-7** On the TV (and at 200 % Windows scaling): the symbols are sharp, the PlayStation icons the same size
      and weight as the letters beside them, round face buttons, pills for the rest; no colour. · E2E: no (look on
      the real screen)
- [ ] **T215-8** Steam running, Steam-Eingabe for PlayStation and Switch on: the DualSense and the Switch Pro show
      the same symbols as with it off. · E2E: no (real Steam and devices)

History:

| Test | Date | Who | Build / environment | Result | Consequence |
|---|---|---|---|---|---|
| T215-1 to T215-6 | 2026-10-28 | Claude | PR branch, Linux container, Avalonia headless, fake SDL pads (acceptance tests) | ✅ | – |
| T215-7 | 2026-10-28 | Claude | PR branch, Linux container, headless screenshots at 1920 × 1080, scaling 1 (DualSense, DualShock 4, Switch Pro, Steam Controller, 8BitDo) | ✅ icons first a size larger than the letters, reduced to 10 px | – |
