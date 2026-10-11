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
