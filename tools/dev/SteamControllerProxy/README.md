# steamproxy239 (spike #239)

A throwaway proxy, not part of Arcadia: it shows Steam an emulated copy of the Steam Controller's puck (`28DE:1304`)
through VIIPER and usbip-win2 and passes every HID report 1:1 between Steam and the real puck, which HidHide hides
from Steam. Feature messages that could change the controller for good (firmware, serial, pairing records, anything
unknown) are swallowed and logged; payloads are never logged.

It uses VIIPER's internal USB server, so it is built inside a VIIPER checkout (GPL-3.0, like Arcadia):

```bash
git clone https://github.com/Alia5/VIIPER && cd VIIPER && git checkout 20aa112c6ea47187526dd88d0fd666a68a5f4416
git apply <arcadia>/tools/dev/SteamControllerProxy/viiper-no-in-repeat.patch
mkdir -p cmd/steamproxy239 && cp <arcadia>/tools/dev/SteamControllerProxy/*.go cmd/steamproxy239/
cp LICENSE.txt internal/install/bundle/licenses.txt   # VIIPER embeds this file; its build normally generates it
GOOS=windows GOARCH=amd64 go build -trimpath -o steamproxy239.exe ./cmd/steamproxy239
go test ./cmd/steamproxy239                            # the platform-independent parts, on any OS
```

Go 1.26 builds it after `go mod edit -go=1.26` (VIIPER asks for 1.27.2 but needs nothing newer).
The patch keeps VIIPER from repeating the last input report when the real device sent none within `bInterval`.

Inputs: `descriptors.json` from `SteamInputProbe describe --json` (the real puck's descriptors, without serial number;
the proxy reads that from the device). Runner on the owner's PC: `Run-Checks-239-2.ps1` (project files, `spikes/239/`).
