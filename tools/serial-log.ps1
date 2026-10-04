# Captures the ESP32's USB debug output with PC timestamps, so seat reboots, brownouts and
# WiFi drops can be lined up against the backend's connection diagnostics log.
# Usage: powershell -File tools\serial-log.ps1 -Port COM3 -OutFile esp-serial.log
param(
    [string]$Port = "COM3",
    [int]$Baud = 38400,
    [Parameter(Mandatory = $true)][string]$OutFile
)

$serial = New-Object System.IO.Ports.SerialPort $Port, $Baud, ([System.IO.Ports.Parity]::None), 8, ([System.IO.Ports.StopBits]::One)
# Keep DTR/RTS low: on ESP32 dev boards they drive EN/GPIO0 and would reset the seat.
$serial.DtrEnable = $false
$serial.RtsEnable = $false
$serial.ReadTimeout = 500
$serial.NewLine = "`n"
$serial.Open()

$writer = New-Object System.IO.StreamWriter($OutFile, $true)
$writer.AutoFlush = $true
$writer.WriteLine("$(Get-Date -Format 'HH:mm:ss.fff') --- serial log started on $Port @ $Baud ---")
try {
    while ($true) {
        try {
            $line = $serial.ReadLine().TrimEnd("`r")
            $writer.WriteLine("$(Get-Date -Format 'HH:mm:ss.fff') $line")
        }
        catch [System.TimeoutException] { }
    }
}
finally {
    $writer.Dispose()
    $serial.Dispose()
}
