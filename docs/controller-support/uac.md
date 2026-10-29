# Answering UAC by controller (spike #118)

Can Windows' UAC consent prompt — which runs on the isolated Secure Desktop, where `SendInput` and Steam's
desktop layout do not reach — be answered with the controller alone, through a VIIPER virtual USB keyboard/mouse?
Overview and the other files: [README](README.md).

**Yes.** A non-elevated process with a VIIPER virtual keyboard/mouse can navigate and confirm the consent prompt.
Decision (owner, go/no-go) and the security trade-off: see the end.

Observed on the owner's PC on 2026-10-26 with the probe `tools/dev/SteamInputProbe` in its `uac` mode and the step
runner `spikes/118/Run-Checks-118.ps1` (project files). Environment: Windows 11, admin account (consent prompt =
Yes/No), usbip-win2 installed, Arcadia/SISR not running (the probe starts `viiper.exe` itself from `%LOCALAPPDATA%\SISR`,
no `SISR.exe`). The probe ran **non-elevated** throughout.

## What was seen

- **VIIPER makes a USB keyboard and mouse from C#, no `SISR.exe`.** The probe created a `keyboard` and a `mouse`
  device over VIIPER's MIT client (TCP API `localhost:3242`, auto-attached by usbip-win2) and both work on the normal
  desktop: it typed "spike" into Notepad and moved the pointer. (VIIPER keyboard `2E8A:0010`, mouse `2E8A:0011`.)
- **The input reaches the Secure Desktop.** On an open consent prompt:
  - **Mouse:** the pointer visibly moved on the prompt.
  - **No:** `Esc` closes the prompt (declines). So does `Enter` — because the prompt **starts focused on "Nein"**,
    and `Enter` activates the default button.
  - **Yes:** `Left arrow` then `Space` confirmed — the elevated process actually started. (`Enter` does not: it hits
    the default "Nein". `Tab`+`Space`, the letter `J`, and `Right`+`Space` did not confirm either. `Space` activates
    the *focused* button, `Left` moves focus from "Nein" to "Ja".)
- **Timing / device kept ready.** The keyboard and mouse are created once and kept attached (no per-prompt setup).
  Keys fired in the very first moment after the prompt appears can be dropped; a key sent after a short settle
  (~0.5 s) is accepted reliably. In the clean run `Left` landed ~0.5 s and `Space` ~1.0 s after detection and
  confirmed. So: keep the device ready, wait ~0.5 s after detecting the prompt, then send.
- **Detecting UAC, apart from lock/sign-in, from a non-elevated process.** When a Secure Desktop is in front,
  `OpenInputDesktop` is denied (the input desktop is no longer "Default"). `consent.exe` running tells a UAC prompt
  apart from the lock/sign-in screen (`LogonUI.exe`, no `consent.exe`). Detection was within one 100 ms poll of the
  prompt appearing; the probe correctly logged `uac` for every consent prompt and `lock/signin` for Win+L.
- **Admin rights:** none needed beyond the already-installed usbip-win2 driver. The probe is not elevated.
- **SDL keeps reading the physical pad while the Secure Desktop is in front.** Confirmed 2026-10-27 with a DualSense
  (`054C:0CE6`, a pure gamepad with no keyboard emulation, so it can't dismiss the prompt itself): with a consent
  prompt open (`DESKTOP: uac`, `consent.exe`), the non-elevated probe's SDL reader logged a continuous stream of
  button presses (D-pad in every direction, A/B/X/Y) the whole time the prompt was up, and stopped when it closed
  (`DESKTOP: interactive`). So Arcadia (non-elevated, default desktop) knows which button the user pressed *during*
  the prompt and can drive the virtual keyboard accordingly. (Reading needs `SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS`;
  see #112.) A keyboard-emulating pad like the 8BitDo must not be used to measure this — its own HID keyboard closes
  the prompt before it can be observed.

## Security finding

Because a VIIPER virtual device is real USB hardware to Windows, **any non-elevated local process that can reach
VIIPER can answer a UAC consent prompt** — the probe did exactly this, with no Arcadia involved. What it needs:

- the usbip-win2 driver present (it is, wherever the Steam Controller support is set up), and
- to talk to VIIPER's API on localhost (localhost auth is **optional by default**; the password, when required, is in
  `%APPDATA%\VIIPER\viiper.key.txt`), or to start `viiper.exe` itself.

This weakens the protection UAC's Secure Desktop is meant to give against software (not physical) input, and it
**already holds today** on any PC where SISR/VIIPER is installed — the UAC feature does not create the exposure, it
only uses it. Hardening option: run VIIPER with localhost auth required (`--api.require-localhost-auth`) so arbitrary
processes cannot drive it. The owner decides whether to accept this, harden it, or drop the route (see below).

## What was read, not observed

- The consent prompt's button layout/focus ("Nein" default, `Left` → "Ja", `Space` activates focus) is standard
  Windows dialog behaviour and matched every run on this PC; other Windows versions/locales may differ and should be
  re-checked when the story is built.
- A physical 8BitDo pad (dongle `2DC8:310B`) could already navigate the same prompt, because its dongle enumerates as
  a **real HID keyboard + mouse** (interface MI_01) on top of the XInput controller (MI_00) — same mechanism, from the
  device's own firmware. (The Valve dongle `28DE:1304` likewise exposes keyboard/mouse collections.)

## Not confirmed yet (for the implementation story)

- Robustness of the "wait ~0.5 s then send" timing across Windows versions, and the exact minimum settle time.

## Story shape (on go)

- A mode that switches on automatically when a UAC consent prompt is detected (independent of the mouse mode #64),
  off for lock/sign-in. Keep a VIIPER keyboard ready; map **D-pad = arrows, A = confirm the focused button (Space),
  B = Esc (No)**; to confirm "Yes": move focus to "Ja" (Left) then A. No Arcadia button hints are shown there.
- Builds on #213 (VIIPER in Arcadia) and #214 (usbip-win2 setup). Credential prompts of standard accounts stay out of
  scope (Arcadia's on-screen keyboard can't appear on the Secure Desktop), as do lock/sign-in and Ctrl+Alt+Del.
