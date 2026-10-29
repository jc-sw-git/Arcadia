# Answering UAC by controller through a VIIPER keyboard (spike #118, story #247)

Windows' UAC consent prompt runs on the Secure Desktop, where `SendInput` and Steam's desktop layout don't reach. A
VIIPER virtual keyboard/mouse is real USB hardware to Windows, so a **non-elevated** process can answer it; verified
on the owner's PC (Windows 11, admin account, usbip-win2 installed). Full spike notes: issue #118.

- The prompt starts focused on "Nein": `Esc` or `Enter` declines; `Left` then `Space` confirms (the elevated process
  started). `Tab`+`Space`, `J` and `Right`+`Space` didn't confirm. Standard dialog behaviour, re-check on other Windows
  versions and locales when the story is built.
- Keep the VIIPER keyboard and mouse created and attached (no per-prompt setup); a key sent in the first moment after
  the prompt appears can be dropped, one sent after ~0.5 s is accepted. Minimum settle time not measured.
- Detecting the prompt from a non-elevated process: `OpenInputDesktop` is denied while a Secure Desktop is in front;
  `consent.exe` running tells UAC apart from the lock/sign-in screen (`LogonUI.exe`). Within one 100 ms poll.
- SDL keeps reading the physical pad while the Secure Desktop is in front (needs
  `SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS`), so Arcadia knows which button was pressed during the prompt. Don't measure
  this with a keyboard-emulating pad (8BitDo): its own HID keyboard closes the prompt.
- **Security:** any local process that can reach VIIPER's API or start `viiper.exe` can answer a UAC prompt this way;
  that is why Arcadia's VIIPER requires localhost auth with its own key (#304).
- Story shape: a mode that switches on when a consent prompt is detected (independent of the mouse mode), off for
  lock/sign-in; D-pad = arrows, A = Space (confirm the focused button), B = Esc; "Yes" = Left then A. Credential
  prompts of standard accounts, lock/sign-in and Ctrl+Alt+Del stay out of scope.
