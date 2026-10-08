# SpeedSeat — Project Guide for Claude

## What this project is

SpeedSeat is a motion-simulator racing seat controller. It drives three stepper-motor axes (FrontLeft, FrontRight, Back) to tilt a seat in real time based on F1 2020 / F1 25 game telemetry or manual input. The system has three parts:

| Layer | Tech | Entry point |
|---|---|---|
| Backend | C# / ASP.NET Core 6 / SignalR | `backend/speedseat.sln` → run `speedseat` project |
| Frontend | Angular 13 / Angular Material / Plotly | `frontend/` → `npm i && npm run start` |
| Microcontroller | C++ / Arduino / PlatformIO (ESP32) | `microcontroller/platformio.ini` |

In production the backend embeds the compiled frontend (`wwwroot/`) as a manifest-embedded resource and serves it from its own Kestrel process on **port 5000**. In development, Angular dev server runs on **port 4200** independently.

---

## Running locally

**Backend** — open `backend/speedseat.sln` in Visual Studio or run:
```
dotnet run --project backend/speedseat.csproj
```

**Frontend (dev mode)**:
```
cd frontend
npm i
npm run start   # http://localhost:4200
```

Both must be running when testing in dev mode. The backend automatically opens the browser in non-development mode.

**Microcontroller** — use PlatformIO (VS Code extension or CLI):
```
cd microcontroller
pio run --target upload
```
Serial monitor speed: **38400 baud**.

---

## Architecture

### Backend (`backend/`)

| File/Folder | Purpose |
|---|---|
| `Program.cs` | App startup, DI registration, SignalR hub mapping, config loading |
| `Processing/Speedseat.cs` | Core seat logic — converts front/side tilt to motor positions, applies response curves |
| `Processing/CommandService.cs` | Connection management (WiFi/UDP only — no USB), 8-byte protocol read/write, ACK handling. Drops the connection on any transmission failure so the auto-connect loop rebinds |
| `Processing/ConnectionManager.cs` | Background service that keeps the backend bound to the seat: while disconnected it discovers the ESP32 over UDP every 2s, connects, runs the firmware check, and pushes connect/disconnect to the frontend via the `connectionStateChanged` event |
| `Processing/UdpConnection.cs` | UDP transport: broadcast discovery of ESP32s (`EspDiscovery`) + `UdpDeviceConnection` implementing `ISerialPortConnection` |
| `Processing/F12020TelemetryAdaptor.cs` | Receives F1 2020 / F1 25 UDP telemetry (port 20777), maps G-forces to tilt |
| `F12025Telemetry/F12025Packets.cs` | F1 25 packet structs (29-byte header used since F1 23); motion data is converted to the F1 2020 shape, parsing selected via `TelemetryGameVersion` |
| `Processing/OutdatedDataDiscardQueue.cs` | Drop-last-value queue to prevent stale motor position commands |
| `Data/SpeedseatSettings.cs` | All user-configurable settings stored in SQLite; exposes `IObservable<T>` for reactive updates |
| `Data/SpeedseatContext.cs` | EF Core SQLite context (`speedseat_dbversion2.sqlite3`) |
| `Data/Config.cs` | Typed representation of `config.json` |
| `Api/*.cs` | SignalR hubs: `ManualControlHub`, `ConnectionHub`, `InfoHub`, `ProgramSettingsHub`, `SeatSettingsHub`, `TelemetryHub` |
| `config.json` | Command definitions (IDs, value ranges, labels). Auto-created from embedded `config_template.json` if missing. |

**DI singletons**: `Speedseat`, `CommandService`, `OutdatedDataDiscardQueue<Command>`, `F12020TelemetryAdaptor`, `SpeedseatSettings`, `FrontendLogger`, `FirmwareUpdateService`, `UpdateCheckService`. **Hosted service**: `ConnectionManager` (auto-connect loop).

**Always-on connection**: there is no manual connect/disconnect/port-select UI. `ConnectionManager` discovers and connects to the ESP32 on its own and reconnects after any failure (seat reboot, WiFi hiccup, OTA restart). The frontend only reflects the pushed `connectionStateChanged` state; while disconnected it shows a "Connecting to seat…" overlay.

**Always-on telemetry**: `F12020TelemetryAdaptor.Start()` is called once in `Program.cs` — telemetry processing runs for the whole backend lifetime, there is no start/stop streaming state. The game is auto-detected per packet from the `packetFormat` header field (= game year); the detected game is pushed to the frontend via the `gameDetected` event on the telemetry hub.

**Self-update** (`Processing/UpdateCheckService.cs` + `Processing/SelfUpdateService.cs`): at boot the backend queries the latest public GitHub release (`ZanyCode/SpeedSeat`) and compares it with its assembly version (set by CI via `-p:Version`); `InfoHub.GetUpdateInfo` exposes the result and the frontend toolbar shows an **Install update** button when one exists. Clicking it calls `InfoHub.InstallUpdate` → `SelfUpdateService`, which downloads the release's `speedseat.exe` next to the running one and swaps it **in place** (rename running exe → `<exe>.old`, move new file into its place, relaunch with `--updated` so it doesn't open a second browser, then `Environment.Exit`; the leftover `.old` is deleted on next boot via `CleanupOldVersion`). This keeps the SQLite DB + `config.json` (both next to the exe) across updates. Progress is pushed via the `updateInstallState` event; the frontend shows a full-screen overlay and reloads onto the new backend. If the in-place swap isn't possible (running under `dotnet run`, missing permissions, failed download) it returns false and the frontend **falls back** to opening the download URL in the browser.

**Firmware OTA** (`Processing/FirmwareUpdateService.cs`): each release embeds the matching ESP32 `firmware.bin` + `firmware_version.txt` (created by CI next to the csproj, see `.gitignore`). After every successful connect the backend sends a version read request (0x40); on mismatch it sends 0x41 and the ESP downloads `http://<pc>:5000/firmware.bin`, flashes it (`Update.h`) and restarts. Progress is pushed via the `firmwareUpdateState` event on the connection hub; the frontend shows a big full-screen overlay during `updating` and `ConnectionManager` reconnects automatically once the seat is back. If the seat doesn't report a version at all, the backend still attempts the update once per run (every firmware in the field supports OTA, so a missing answer means a stuck or lost handshake, not an old seat); only if the version is still missing after that does it ask for a USB flash.

**Releases must tolerate each other's commands**: a seat is always on a different firmware release than the backend right before an update, so the MC may report settings the backend doesn't know (added later or removed again). The backend acknowledges unknown command IDs with `0xFF` and ignores them — answering `0xFE` made older firmwares resend the command forever, which blocked their version report and with it the update (release 0.2.74 hit this after command 24 was removed). The firmware in turn gives up a rejected value after `MAX_RESEND_ATTEMPTS`.

**USB flashing** (`Processing/UsbFlashService.cs`): for first-time setup or recovery — when a seat has no (compatible) firmware and so can't be reached over WiFi — the backend can flash it over USB. Packaged release builds embed a standalone `esptool.exe` plus the full ESP32 image set (`bootloader.bin`, `partitions.bin`, `boot_app0.bin`, `firmware.bin`); at runtime they're written to a temp dir and esptool flashes them at the standard offsets (0x1000/0x8000/0xe000/0x10000) to each detected COM port until one succeeds. The frontend shows this only when the seat stays disconnected for ~5s: a help panel explains the first-time WiFi-portal setup and offers a **Flash via USB** button (`ConnectionHub.FlashViaUsb`, gated by `GetCanFlashViaUsb`); progress is pushed via the `usbFlashState` event. This path is independent of the WiFi/UDP protocol (esptool drives the COM port directly; `CommandService` never does). After a successful flash the seat reboots into the `SpeedSeat-Setup` portal for WiFi setup.

### Frontend (`frontend/src/app/`)

Angular SPA with Angular Material UI. Main views:
- **ManualControl** — direct slider control of motor positions
- **SeatSettings** — per-command settings read from `config.json` (numeric/boolean/action widgets)
- **ProgramSettings** — response curve editor, telemetry multipliers/caps, motor index mapping
- **Telemetry** — live Plotly chart of front/side tilt; the source game (F1 2020–2025) is detected automatically from the incoming packets and shown as a status, streaming is always on (no buttons). Its numeric settings (multipliers, caps, acceleration boost) are locked until their lock button is clicked, like the seat settings

All backend communication is over SignalR (not REST). Hub URLs: `/hub/manual`, `/hub/connection`, `/hub/info`, `/hub/programSettings`, `/hub/seatSettings`, `/hub/telemetry`.

### Microcontroller (`microcontroller/`)

Target: **AZ-Delivery DevKit v4 (ESP32)**. Built with PlatformIO.

- `src/main.cpp` — setup/loop, dispatches commands to X/Y/Z Axis objects
- `src/Axis.cpp` + `include/Axis*.h` — stepper axis: homing, movement, EEPROM load/save
- `src/communication.cpp` + `include/communication.h` — 8-byte protocol implementation (transport-agnostic)
- `src/transport.cpp` + `include/transport.h` — byte-stream transport abstraction: `UdpTransport` only (WiFi/AsyncUDP + discovery responder). WiFi is brought up via the WiFiManager captive portal (`lib/WiFiManager`, tzapu). USB-serial transport was removed
- `src/smoothy.cpp` — motion smoothing/filter
- `include/configuration.h` — compile-time flags (see below)
- `include/pins.h` — ESP32 GPIO pin assignments

**Axes → motors**:
- X_Axis = FrontLeft/FrontRight (side tilt — mapped by `FrontLeftMotorIdx`/`FrontRightMotorIdx`)
- Y_Axis = second side motor
- Z_Axis = Back motor

**Motor physical constants** (in `configuration.h`):
- Steps/rotation: 1600 (800 microstep × 2)
- Lead screw: 4 mm/rotation × 4:1 gear = 16 mm/rotation
- `STEPS_PER_MM` = 100

---

## Communication Protocol (WiFi/UDP only)

8-byte fixed-length packets, transported over **WiFi/UDP**. USB-serial is no longer a transport — the backend never opens a COM port, and the ESP only uses USB serial for debug logging. (The MC monitor speed is still 38400 baud for those logs.)

**UDP transport**:
- The ESP32 obtains WiFi credentials through the **WiFiManager captive portal** (no hard-coded SSID/password): on first boot, or whenever the saved network is unreachable, it opens an open access point named **`SpeedSeat-Setup`**; connect to it and pick your network. After that it auto-connects on every boot. It listens on UDP port **8888** (`SpeedseatUdpProtocol.Port` in backend ↔ `UDP_PORT` in firmware — keep in sync).
- **Discovery handshake**: backend broadcasts `SPEEDSEAT_DISCOVERY` (to 255.255.255.255 and every interface's directed broadcast); each ESP replies `SPEEDSEAT_ESP32`. `ConnectionManager` discovers IPs this way and connects to the first responder; the `DeviceConnectionFactory` always creates a `UdpDeviceConnection`.
- After discovery, all traffic is **unicast** (WiFi broadcast frames are slow/unreliable). Each 8-byte command and each ACK byte is one datagram. The ESP replies to the endpoint of the last received protocol datagram.
- ESP32 quirks: must use **AsyncUDP** (WiFiUDP can't receive broadcasts) and must call **`WiFi.setSleep(false)`** after connecting (modem sleep causes burst latency).

| Byte | Content |
|---|---|
| 0 | Command ID byte. LSB=1 → read request; LSB=0 → write request. Actual ID = byte >> 1 |
| 1–2 | Value 1 (MSB first) |
| 3–4 | Value 2 (MSB first) |
| 5–6 | Value 3 (MSB first) |
| 7 | XOR hash of bytes 0–6 |

**Responses** (single byte):
- `0xFF` = SUCCESS (valid hash, accepted)
- `0xFE` = INVALID HASH

**Reserved command IDs** (never reuse in `config.json`):

| ID | Direction | Meaning |
|---|---|---|
| 0x00 | Both | Motor positions (Value1=X/FrontRight, Value2=Y/FrontLeft, Value3=Z/Back) |
| 0x01 | PC→MC | Start init (sent after connection opens) |
| 0x02 | MC→PC | Init finished (MC signals readiness) |
| 0x40 | PC→MC read, MC→PC write | Firmware version handshake: PC sends a read request after connect, MC answers with `FW_VERSION_NUMBER` in Value1. Old firmwares NACK → backend treats version as unknown/outdated |
| 0x41 | PC→MC | Start OTA firmware update; Value1 = HTTP port of the backend (5000). ESP downloads `/firmware.bin` from the sender IP, flashes, restarts (UDP/WiFi builds only) |
| 0x42 | PC→MC | Reset EEPROM (still handled by the MC; the backend no longer sends it — the Reset-EEPROM UI was removed) |

**Motor position stream (0x00) is fire-and-forget**: `CommandService` sends every motor position the moment it exists and neither waits for its ack nor resends it — the next, newer position replaces a lost one (the way games stream state). The MC still acks each position; the backend only uses "anything received" as a liveness signal and drops the connection after 60 positions in a row without any reply (~1.5 s). All other commands keep the 3-attempt retry with `commandSendRetryIntervalMs`; because acks carry no ID they wait 80 ms after the last position so a position ack can't be mistaken for theirs.

**MC receive framing**: `communication::execute()` frames the byte stream by content — a lone `0xFF`/`0xFE` between commands is an answer (no command ID byte has that value), anything else is an 8-byte command — and takes one command per pass. Commands therefore may arrive back to back (they do after every WiFi stall); the old "read everything, then count bytes" logic treated that as overflow and discarded them.

**Connection debugger** (`Processing/ConnectionDiagnostics.cs`): set the env var `SPEEDSEAT_DIAG_DIR` to a directory and the backend writes a timestamped event log there (every TX/ACK/timeout with round-trip time, every RX datagram, connects/disconnects, telemetry rate, ICMP ping to seat + gateway, PC WiFi state, process stalls). `SPEEDSEAT_DIAG_GATEWAY` overrides the pinged gateway IP. Without the env var it is a no-op. `tools/serial-log.ps1` captures the ESP's USB debug output with matching timestamps. The seat's position reports (`RX` of command 12, mm per axis) can be compared against the commanded positions (`TX` of command 0) to tell a motion bug from a network or telemetry problem.

**Connection sequence**: PC sends 0x01 → MC performs sync (read/write requests) → MC sends 0x02 → UI unblocks → backend runs the firmware version handshake (0x40, possibly 0x41) in the background.

---

## Configuration (`config.json`)

Loaded at startup; hot-reloaded via `IOptionsMonitor<Config>`. Located next to the executable. If missing or invalid, recreated from the embedded `config_template.json`.

Each command entry defines:
- `id` — integer (must be unique, must not be a reserved ID)
- `groupLabel` — display name in the UI
- `readonly` — if true, MC can only push values to PC; PC will not save them
- `value1/2/3` — type (`numeric`/`boolean`/`action`), label, min/max/default, `scaleToFullRange`

`scaleToFullRange: true` means the 0–1 float is mapped to 0–0xFFFF for transmission.

---

## Microcontroller compile-time flags (`configuration.h`)

| Flag | Effect |
|---|---|
| _(WiFi/UDP is always on)_ | The seat always talks to the PC over WiFi/UDP — there is no USB-serial transport and no `USE_UDP` flag. WiFi credentials come from the WiFiManager `SpeedSeat-Setup` captive portal (not hard-coded); `UDP_PORT` is defined in `configuration.h` |
| `FW_VERSION_NUMBER` | Numeric firmware version reported in the 0x40 handshake. Defaults to 0 (dev build); CI overrides it with the release build number via `PLATFORMIO_BUILD_FLAGS` |
| `NO_HARDWARE` | Skips real motor control; useful for software-only testing |
| `USE_EEPROM` | Loads/saves axis settings from ESP32 EEPROM on boot/save command |
| `AUTO_RETURN_TO_ZERO` | Seat returns to centre when telemetry FPS drops to 0 for 200 ms |
| `AUTO_SAVE` | Auto-saves to EEPROM on every change (off by default) |
| `ANALYZE_MOTION_CERNEL` | Sends random move commands for stress-testing |
| `DEBUG` | Enables `printPosition()` serial debug output |
| `LINK_DIAGNOSTICS` | WiFi link debugging (`src/linkdiag.cpp`): the ESP pings the WiFi gateway every 40 ms in a background task and prints slow/lost replies, RSSI and WiFi disconnects to USB serial (`DIAG ...` lines). Off by default |

---

## Key settings (`SpeedseatSettings`)

All persisted in SQLite. Properties expose `IObservable<T>` variants (`*Obs`) for reactive subscriptions.

- `FrontLeftMotorIdx` / `FrontRightMotorIdx` / `BackMotorIdx` — which array slot (0/1/2) maps to which motor
- `BackMotorResponseCurve` / `SideMotorResponseCurve` — piecewise-linear curves applied before sending positions
- `FrontTiltPriority` — how much front tilt reduces side tilt when both are at max
- `FrontTiltAccelerationBoost` — extra factor on positive longitudinal G (accelerating) only, applied before the multiplier; braking is unaffected. 1 = off. Shown as "Acceleration Boost (%)" (100–1000) in the Telemetry view
- `FrontTilt/SideTilt GforceMultiplier`, `OutputCap`, `Smoothing`, `Reverse` — telemetry scaling. Default multipliers are 0.07 (front) and 0.34 (side); the former 0.3/0.3 was far too aggressive on the front axis

(The former `TelemetryGameVersion` setting is gone — the game is auto-detected from the telemetry packets.)

---

## Build & release

**Every push to `main` creates a GitHub release** (`.github/workflows/build-release.yml`): version `0.2.<run_number>.0`, firmware version `<run_number>`. The workflow builds the frontend (Angular outputs directly to `backend/wwwroot/`), builds the ESP32 firmware with `FW_VERSION_NUMBER` set, copies `firmware.bin`/`firmware_version.txt` into `backend/`, copies the rest of the USB-flash payload (`bootloader.bin`, `partitions.bin`, `boot_app0.bin`) and downloads the standalone Windows `esptool.exe` into `backend/` (all gitignored, embedded by the csproj), then publishes the single-file self-contained `speedseat.exe` with `-p:Version`. Release assets: `speedseat.exe` and `firmware.bin`.

Backend, frontend and firmware must always be released together — the update chain (GitHub release check → exe download → firmware OTA on next connect) assumes their versions match.

---

## Debugging connection hiccups (findings from 2026-10-04)

**Symptom**: while driving, the seat froze for about a second every 10–20 s and drifted to centre.

**Cause 1 — protocol (fixed)**: motor positions were sent stop-and-wait with the generic 1000 ms retry interval. One lost or late WiFi datagram (measured ~0.2 % of packets) blocked every newer position for a full second, and after 200 ms without commands the firmware's `AUTO_RETURN_TO_ZERO` moved the seat to centre. First fix (2026-10-04): 60 ms ack window, no resend — freezes of ~1000 ms disappeared. Second step (2026-10-08): positions are not waited on at all (see "Motor position stream" above), because during a half-broken link each lost packet still cost 60 ms+ and added up to 150–310 ms gaps.

**Cause 2 — network (not fixable in code)**: the remaining late packets came from the WiFi access point, here an Android phone hotspot. Every ~10.7 s it stalled for ~0.8 s, raising latency for *all* clients from 4–8 ms to 30–70 ms (occasionally 100–360 ms). Proven by elimination:
- PC → access point ping alone shows the bursts (seat not involved).
- ESP → access point ping (`LINK_DIAGNOSTICS` firmware) shows the same bursts at the same timestamps (PC not involved).
- Bluetooth off on the PC: no change. Windows scan list not refreshing: not a PC-side network scan.
- Nothing on the ESP serial log (no reboot/brownout), RSSI ≈ -43 dBm, PC signal 91 %.

A phone hotspot is one radio doing two jobs; it leaves the hotspot channel when the phone scans for networks. A dedicated router (no internet needed) for PC + seat is the proper setup. The hotspot also sat on 2.4 GHz channel 10, overlapping neighbours on 6/7.

**Cause 3 — PC on the hotspot's 5 GHz band (2026-10-08)**: with the PC on the hotspot's 5 GHz access point (channel 161) the PC ↔ phone link dropped out for ~3.5 s at a time, at spacings that are multiples of ~62 s (about half the packets lost, felt as a series of ~250 ms hiccups). During one such outage the ESP's own pings to the phone (2.4 GHz) were normal, so the failing hop is PC ↔ phone on 5 GHz; whether the PC's Intel 3160 card or the phone's 5 GHz radio is at fault is not determined. No such outages were seen with the PC on 2.4 GHz. `tools/wifi-connect-bssid.ps1 -Profile <ssid> -Band 2.4` pins the PC to the 2.4 GHz access point without admin rights (only for the current connection).

**Firmware motion bugs found with the same logs (fixed 2026-10-08)**:
- *One corner hangs for up to a second* (`Axis::_move`, `AxisMove.h`): the accelerate/decelerate decision was only taken when a step was made. An axis that had braked to a crawl has its next step up to 1 s away and ignored a new, distant target until then. It now switches back to accelerating between steps as soon as the target lies beyond the braking distance.
- *Kick shortly after telemetry starts* (`Axis::moveAbsoluteSteps` / `Smoothy`): the smoothing filter is only used while `gamingActive` (more than 5 commands/s, evaluated once per second). While bypassed its buffer kept the positions of the previous session (zeros after boot), so the first filtered outputs lunged toward those. The buffer now follows the unfiltered position while the filter is bypassed.
- The "Gentle acceleration near target" setting (command 24) was removed again.

**How to repeat the investigation**:
1. Run the backend with `SPEEDSEAT_DIAG_DIR=<dir>` (and `SPEEDSEAT_DIAG_GATEWAY=<AP IP>` when the AP is not the IPv4 default gateway, as with phone hotspots). Look for `TIMEOUT` lines and for gaps between `ACK` lines; compare `PING seat` against `PING gateway` — both slow at once means PC/AP side, only the seat slow means seat side.
2. `tools/serial-log.ps1 -Port COMx -OutFile <file>` records the ESP's USB output with PC timestamps (keeps DTR/RTS low so the ESP is not reset).
3. `tools/dense-ping.ps1 -Target <AP IP> -Seconds 60` reveals periodic latency bursts and their period.
4. `tools/bluetooth-radio.ps1 -State Off|On` toggles the PC's Bluetooth radio without admin rights to rule out WiFi/Bluetooth coexistence.
5. Build the firmware with `LINK_DIAGNOSTICS` and flash via USB (`pio run --target upload --upload-port COMx`; stop the serial logger first, it holds the port) to get the ESP's own view of the AP.

**Local dev-run gotchas found on the way**:
- Only the .NET 7 runtime may be installed while the project targets net6: run with `DOTNET_ROLL_FORWARD=Major` (also for `dotnet test`).
- The SQLite DB path is relative to the **working directory**, not the exe. `dotnet run` from `backend/` therefore starts with default settings; copy `speedseat_dbversion2.sqlite3` from the folder the installed exe runs in (its shortcut's working directory) to test with the real settings.
- `dotnet run` uses the Development environment: no app window opens, use http://localhost:5000.
- A dev backend has no bundled firmware and skips the OTA check; a seat flashed with a local firmware (version 0) is updated back to the release firmware by the next release exe that connects.
- `Command.MotorPositionCommandId` (0) commands take the best-effort path in `CommandService` — tests of the retry logic must use another command ID.

---

## Keep this file updated

Update this file when:
- New SignalR hubs or command IDs are added
- Motor wiring / axis mapping changes
- New compile-time flags are added to `configuration.h`
- The serial protocol changes
- New major frontend views or settings are added
- The release/build process changes
