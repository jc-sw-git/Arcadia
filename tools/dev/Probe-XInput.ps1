<#
.SYNOPSIS
    Prints the XInput slots (0–3) and every change of their button state for a while.

.DESCRIPTION
    A process that was not started by Steam only sees the Steam Controller as XInput device
    while SISR emulates it (VIIPER Xbox 360 device). Use this to check that the virtual
    controller exists and to look for spurious button presses.

.EXAMPLE
    ./tools/dev/Probe-XInput.ps1 -Seconds 15
#>
param([int] $Seconds = 10)

Add-Type -Namespace ArcadiaProbe -Name XInput -MemberDefinition '[DllImport("xinput1_4.dll")] public static extern uint XInputGetState(uint index, byte[] state);'
$last = @{}
$clock = [Diagnostics.Stopwatch]::StartNew()
while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
    for ($slot = 0; $slot -lt 4; $slot++) {
        $state = New-Object byte[] 16
        $text = if ([ArcadiaProbe.XInput]::XInputGetState($slot, $state) -eq 0) {
            'buttons=0x{0:X4} lt={1} rt={2}' -f ([BitConverter]::ToUInt16($state, 4)), $state[6], $state[7]
        } else { 'disconnected' }
        if ($last[$slot] -ne $text) {
            '{0,6} ms  slot {1}: {2}' -f $clock.ElapsedMilliseconds, $slot, $text
            $last[$slot] = $text
        }
    }
    Start-Sleep -Milliseconds 5
}
