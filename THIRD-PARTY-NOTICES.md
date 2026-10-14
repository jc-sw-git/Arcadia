# Third-party software

## SISR — Steam Input System Redirector

Arcadia downloads and runs SISR as a separate program for its "Steam Controller Support".
SISR is not part of Arcadia's source code and is not modified by Arcadia.

- Author: Peter Repukat (Alia5)
- License: GPL-3.0
- Source: https://github.com/Alia5/SISR
- Installed to: `%LOCALAPPDATA%\SISR`, SISR's own install location (downloaded from the official GitHub releases, SHA-256 verified)

SISR ships with VIIPER (https://github.com/Alia5/VIIPER) and SDL3 (https://libsdl.org), under their own licenses.

## usbip-win2

Installed on request for the Steam Controller Support (SISR/VIIPER need it).

- Author: Vadym Hrynchyshyn
- License: GPL-3.0
- Source: https://github.com/vadimgrn/usbip-win2

## Velopack

Arcadia's installer and self-update. Its library is part of Arcadia; its `Update.exe` is installed next to Arcadia.

- License: MIT
- Source: https://github.com/velopack/velopack

## mattpocock/skills

The Claude skills `.claude/skills/refinement` and `.claude/skills/diagnose` (incl. `hitl-loop.template.ps1`)
are adapted from `grilling`, `to-tickets` and `diagnosing-bugs`. Development tooling only, not shipped with Arcadia.

- Author: Matt Pocock
- License: MIT
- Source: https://github.com/mattpocock/skills

```text
MIT License

Copyright (c) 2026 Matt Pocock

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
