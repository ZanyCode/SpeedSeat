# Switches the PC's Bluetooth radio on or off (same as the toggle in Windows settings, no admin
# needed). Used to test whether Bluetooth is disturbing the WiFi link to the seat.
# Usage: powershell -File tools\bluetooth-radio.ps1 -State Off
param([Parameter(Mandatory = $true)][ValidateSet('On', 'Off')][string]$State)

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($winRtTask, $resultType) {
    $netTask = $asTaskGeneric.MakeGenericMethod($resultType).Invoke($null, @($winRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}

[Windows.Devices.Radios.Radio, Windows.System.Devices, ContentType = WindowsRuntime] | Out-Null
[Windows.Devices.Radios.RadioAccessStatus, Windows.System.Devices, ContentType = WindowsRuntime] | Out-Null
[Windows.Devices.Radios.RadioState, Windows.System.Devices, ContentType = WindowsRuntime] | Out-Null

Await ([Windows.Devices.Radios.Radio]::RequestAccessAsync()) ([Windows.Devices.Radios.RadioAccessStatus]) | Out-Null
$radios = Await ([Windows.Devices.Radios.Radio]::GetRadiosAsync()) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]])
$bluetooth = $radios | Where-Object { $_.Kind -eq 'Bluetooth' }
if (-not $bluetooth) { "No Bluetooth radio found"; exit 1 }

"Bluetooth was: $($bluetooth.State)"
$result = Await ($bluetooth.SetStateAsync($State)) ([Windows.Devices.Radios.RadioAccessStatus])
"Set to $State -> $result, now: $($bluetooth.State)"
