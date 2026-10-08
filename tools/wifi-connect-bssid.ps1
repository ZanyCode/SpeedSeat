# Reconnects the PC's WiFi to one specific access point (BSSID) of a saved network, e.g. to pin
# the PC to the 2.4 GHz radio of a dual-band hotspot. Needs no admin rights. The pin only lasts
# for this connection: after a disconnect Windows chooses the band itself again.
# Usage: powershell -File tools\wifi-connect-bssid.ps1 -Profile Rompel -Band 2.4
#        powershell -File tools\wifi-connect-bssid.ps1 -Profile Rompel -Bssid 76:7e:50:cb:2c:e0
param(
    [Parameter(Mandatory = $true)][string]$Profile,
    [string]$Bssid,
    [ValidateSet('2.4', '5')][string]$Band = '2.4'
)

if (-not $Bssid) {
    # Pick the BSSID of the wanted band from the scan list (channel <= 14 means 2.4 GHz).
    $inNetwork = $false; $current = $null
    foreach ($line in (netsh wlan show networks mode=bssid)) {
        if ($line -match '^SSID \d+ : (.*)$') { $inNetwork = ($Matches[1].Trim() -eq $Profile) }
        elseif ($inNetwork -and $line -match 'BSSID\w* \d+\s*: ([0-9a-fA-F:]{17})') { $current = $Matches[1] }
        elseif ($inNetwork -and $current -and $line -match '^\s*(Kanal|Channel)\s*: (\d+)') {
            $is24 = [int]$Matches[2] -le 14
            if (($Band -eq '2.4') -eq $is24) { $Bssid = $current }
            $current = $null
        }
    }
    if (-not $Bssid) { "No $Band GHz access point of '$Profile' in the scan list"; exit 1 }
}
"Connecting to $Profile via $Bssid"

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WlanPin
{
    [StructLayout(LayoutKind.Sequential)]
    struct WLAN_CONNECTION_PARAMETERS
    {
        public int wlanConnectionMode;
        [MarshalAs(UnmanagedType.LPWStr)] public string strProfile;
        public IntPtr pDot11Ssid;
        public IntPtr pDesiredBssidList;
        public int dot11BssType;
        public uint dwFlags;
    }

    [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr handle, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern uint WlanConnect(IntPtr handle, ref Guid interfaceGuid, ref WLAN_CONNECTION_PARAMETERS parameters, IntPtr reserved);

    public static uint Connect(Guid interfaceGuid, string profile, byte[] bssid)
    {
        uint version; IntPtr handle;
        uint result = WlanOpenHandle(2, IntPtr.Zero, out version, out handle);
        if (result != 0) return result;

        // DOT11_BSSID_LIST: NDIS_OBJECT_HEADER (type 0x80, revision 1, size), entry count,
        // total count, then the 6-byte MAC addresses.
        const int size = 20;
        IntPtr list = Marshal.AllocHGlobal(size);
        try
        {
            for (int i = 0; i < size; i++) Marshal.WriteByte(list, i, 0);
            Marshal.WriteByte(list, 0, 0x80);
            Marshal.WriteByte(list, 1, 1);
            Marshal.WriteInt16(list, 2, (short)size);
            Marshal.WriteInt32(list, 4, 1);
            Marshal.WriteInt32(list, 8, 1);
            Marshal.Copy(bssid, 0, IntPtr.Add(list, 12), 6);

            var parameters = new WLAN_CONNECTION_PARAMETERS
            {
                wlanConnectionMode = 0, // connect using a saved profile
                strProfile = profile,
                pDot11Ssid = IntPtr.Zero,
                pDesiredBssidList = list,
                dot11BssType = 1, // infrastructure
                dwFlags = 0
            };
            return WlanConnect(handle, ref interfaceGuid, ref parameters, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeHGlobal(list);
            WlanCloseHandle(handle, IntPtr.Zero);
        }
    }
}
'@

$guid = (Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -match 'Wireless|Wi-?Fi|WLAN|802\.11' } | Select-Object -First 1).InterfaceGuid
if (-not $guid) { "No connected WiFi adapter found"; exit 1 }
$bytes = [byte[]]($Bssid -split '[:-]' | ForEach-Object { [Convert]::ToByte($_, 16) })
$result = [WlanPin]::Connect([Guid]$guid, $Profile, $bytes)
"WlanConnect returned $result (0 = accepted)"
