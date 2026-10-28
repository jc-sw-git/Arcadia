# Third-party software

## SISR — Steam Input System Redirector

Arcadia downloads and runs SISR as a separate program for its "Steam Controller Support".
SISR is not part of Arcadia's source code and is not modified by Arcadia.

- Author: Peter Repukat (Alia5)
- License: GPL-3.0
- Source: https://github.com/Alia5/SISR
- Installed to: `%LOCALAPPDATA%\SISR`, SISR's own install location (downloaded from the official GitHub releases, SHA-256 verified)

SISR ships with VIIPER (https://github.com/Alia5/VIIPER) and SDL3 (https://libsdl.org), under their own licenses.

## VIIPER (Arcadia's fork)

Arcadia ships `viiper.exe`, built from its fork of VIIPER with the device type `hidproxy` for the copy of the Steam
Controller's puck (#242), and runs it as a separate program. The fork's commit and the file's SHA-256 are pinned in
`src/Arcadia.ControllerSupport/Viiper.targets`; the fork's release carries the licenses of its Go modules.

- Author: Peter Repukat (Alia5); fork: jc-sw-git
- License: GPL-3.0
- Source: https://github.com/Alia5/VIIPER, fork https://github.com/jc-sw-git/VIIPER (branch `arcadia`)

## usbip-win2

Installed on request for the Steam Controller Support (SISR/VIIPER need it).

- Author: Vadym Hrynchyshyn
- License: GPL-3.0
- Source: https://github.com/vadimgrn/usbip-win2

## SDL3

Arcadia reads every controller through SDL (#72). The official, unmodified `SDL3.dll` (Windows x64, version pinned in
`src/Arcadia.Input/Sdl3.targets`) is installed next to Arcadia.exe.

- Copyright (C) 1997-2026 Sam Lantinga
- License: zlib
- Source: https://libsdl.org, https://github.com/libsdl-org/SDL

```
This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
```

## Velopack

Arcadia's installer and self-update. Its library is part of Arcadia; its `Update.exe` is installed next to Arcadia.

- License: MIT
- Source: https://github.com/velopack/velopack

## Svg.Skia

Reads SVG files the user picks as launcher logos (#138). Its library and its dependencies (Svg.Model, ShimSkiaSharp,
ExCSS) are part of Arcadia.

- Author: Wiesław Šoltés
- License: MIT
- Source: https://github.com/wieslawsoltes/Svg.Skia

## Libraries in Arcadia's program files

Besides Velopack and Svg.Skia above, these libraries are part of every Arcadia build and installer. All are under the
MIT licence (text below).

| Library | Copyright | Source |
|---|---|---|
| .NET runtime and libraries (self-contained) | .NET Foundation and Contributors | https://github.com/dotnet/runtime |
| Avalonia (incl. Avalonia.Desktop, .Themes.Fluent, .Skia, .HarfBuzz, .Win32, .FreeDesktop, .X11, .Native) | The AvaloniaUI Project | https://github.com/AvaloniaUI/Avalonia |
| CommunityToolkit.Mvvm | .NET Foundation and Contributors | https://github.com/CommunityToolkit/dotnet |
| SkiaSharp | Xamarin, Inc.; Microsoft Corporation | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp | Xamarin, Inc.; Microsoft Corporation | https://github.com/mono/SkiaSharp |
| MicroCom.Runtime | Nikita Tsukanov | https://github.com/kekekeks/MicroCom |
| Tmds.DBus.Protocol | Tom Deseyn | https://github.com/tmds/Tmds.DBus |
| ExCSS | Tyler Brinks | https://github.com/TylerBrinks/ExCSS |
| Viiper.Client (VIIPER's C# client; drives `viiper.exe` for the Steam Controller's Xbox pad, #213) | Peter Repukat | https://github.com/Alia5/VIIPER |

Native parts with their own licences:

- **Skia** (inside `libSkiaSharp.dll`): BSD-3-Clause, Copyright Google Inc. Skia's own dependencies (e.g. libpng,
  zlib, FreeType, libjpeg-turbo, libwebp, expat) are listed with their licences in SkiaSharp's repository.
- **HarfBuzz** (inside `libHarfBuzzSharp.dll`): the "Old MIT" licence, Copyright Google, Inc., Red Hat, Inc. and others
  (https://github.com/harfbuzz/harfbuzz/blob/main/COPYING).
- **ANGLE** (`av_libglesv2.dll`, from Avalonia.Angle.Windows.Natives): BSD-3-Clause, Copyright The ANGLE Project Authors
  (https://chromium.googlesource.com/angle/angle/+/main/LICENSE).
- **Inter** (the font, from Avalonia.Fonts.Inter): SIL Open Font License 1.1, Copyright The Inter Project Authors
  (https://github.com/rsms/inter/blob/master/LICENSE.txt).

MIT licence, as it applies to each library above with its own copyright line:

```text
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

BSD-3-Clause licence, as it applies to Skia and ANGLE with their copyright lines:

```text
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following
   disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following
   disclaimer in the documentation and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote
   products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## Launcher logos

The Steam and Epic Games logos in `SteamIntegration` and `EpicIntegration` are drawn from the paths of simple-icons
16.35.0 (https://github.com/simple-icons/simple-icons, CC0 1.0). The names and logos are trademarks of Valve Corporation
and Epic Games, Inc. They are used unchanged and only to name the launcher; they are not covered by Arcadia's GPL and
are replaced on request of their owner.

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
