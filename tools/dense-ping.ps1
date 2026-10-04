# Pings a host as fast as it answers (min. every 15 ms) and prints one line per reply that is
# slower than -SlowMs, plus a summary. Used to find periodic WiFi latency spikes and their period.
# Usage: powershell -File tools\dense-ping.ps1 -Target 10.77.155.203 -Seconds 60
param(
    [Parameter(Mandatory = $true)][string]$Target,
    [int]$Seconds = 60,
    [int]$SlowMs = 25
)

$ping = New-Object System.Net.NetworkInformation.Ping
$clock = [System.Diagnostics.Stopwatch]::StartNew()
$count = 0; $slow = 0; $lost = 0
while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
    $sentAt = $clock.Elapsed.TotalSeconds
    $reply = $ping.Send($Target, 1000)
    $count++
    if ($reply.Status -ne 'Success') {
        $lost++
        "{0,8:F2}s LOST" -f $sentAt
    }
    elseif ($reply.RoundtripTime -ge $SlowMs) {
        $slow++
        "{0,8:F2}s {1}ms" -f $sentAt, $reply.RoundtripTime
    }
    Start-Sleep -Milliseconds 15
}
"total=$count slow(>=${SlowMs}ms)=$slow lost=$lost"
