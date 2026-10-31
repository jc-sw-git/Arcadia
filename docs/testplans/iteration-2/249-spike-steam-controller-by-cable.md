# #249 Spike: hide and copy the new Steam Controller by cable

No test cases: a spike with no change to Arcadia (only the spike proxy in `tools/dev/` learnt single-interface
devices). Its check script ran on the owner's PC on 2026-10-31 (Windows 11, new Steam Controller by USB cable
`28DE:1302` with the puck `28DE:1304` plugged in, HidHide, usbip-win2, Steam running, Arcadia closed); what it showed
is in `docs/controller-support/emulated-steam-controller.md`. The implementation issue brings its own tests.
