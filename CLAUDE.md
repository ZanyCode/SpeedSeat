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

**Firmware OTA** (`Processing/FirmwareUpdateService.cs`): each release embeds the matching ESP32 `firmware.bin` + `firmware_version.txt` (created by CI next to the csproj, see `.gitignore`). After every successful connect the backend sends a version read request (0x40); on mismatch it sends 0x41 and the ESP downloads `http://<pc>:5000/firmware.bin`, flashes it (`Update.h`) and restarts. Progress is pushed via the `firmwareUpdateState` event on the connection hub; the frontend shows a big full-screen overlay during `updating` and `ConnectionManager` reconnects automatically once the seat is back. Pre-OTA firmwares (that NACK the 0x40 handshake) get a message to flash once manually via USB.

**USB flashing** (`Processing/UsbFlashService.cs`): for first-time setup or recovery — when a seat has no (compatible) firmware and so can't be reached over WiFi — the backend can flash it over USB. Packaged release builds embed a standalone `esptool.exe` plus the full ESP32 image set (`bootloader.bin`, `partitions.bin`, `boot_app0.bin`, `firmware.bin`); at runtime they're written to a temp dir and esptool flashes them at the standard offsets (0x1000/0x8000/0xe000/0x10000) to each detected COM port until one succeeds. The frontend shows this only when the seat stays disconnected for ~5s: a help panel explains the first-time WiFi-portal setup and offers a **Flash via USB** button (`ConnectionHub.FlashViaUsb`, gated by `GetCanFlashViaUsb`); progress is pushed via the `usbFlashState` event. This path is independent of the WiFi/UDP protocol (esptool drives the COM port directly; `CommandService` never does). After a successful flash the seat reboots into the `SpeedSeat-Setup` portal for WiFi setup.

### Frontend (`frontend/src/app/`)

Angular SPA with Angular Material UI. Main views:
- **ManualControl** — direct slider control of motor positions
- **SeatSettings** — per-command settings read from `config.json` (numeric/boolean/action widgets)
- **ProgramSettings** — response curve editor, telemetry multipliers/caps, motor index mapping
- **Telemetry** — live Plotly chart of front/side tilt; the source game (F1 2020–2025) is detected automatically from the incoming packets and shown as a status, streaming is always on (no buttons)

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

---

## Key settings (`SpeedseatSettings`)

All persisted in SQLite. Properties expose `IObservable<T>` variants (`*Obs`) for reactive subscriptions.

- `FrontLeftMotorIdx` / `FrontRightMotorIdx` / `BackMotorIdx` — which array slot (0/1/2) maps to which motor
- `BackMotorResponseCurve` / `SideMotorResponseCurve` — piecewise-linear curves applied before sending positions
- `FrontTiltPriority` — how much front tilt reduces side tilt when both are at max
- `FrontTilt/SideTilt GforceMultiplier`, `OutputCap`, `Smoothing`, `Reverse` — telemetry scaling

(The former `TelemetryGameVersion` setting is gone — the game is auto-detected from the telemetry packets.)

---

## Build & release

**Every push to `main` creates a GitHub release** (`.github/workflows/build-release.yml`): version `0.2.<run_number>.0`, firmware version `<run_number>`. The workflow builds the frontend (Angular outputs directly to `backend/wwwroot/`), builds the ESP32 firmware with `FW_VERSION_NUMBER` set, copies `firmware.bin`/`firmware_version.txt` into `backend/`, copies the rest of the USB-flash payload (`bootloader.bin`, `partitions.bin`, `boot_app0.bin`) and downloads the standalone Windows `esptool.exe` into `backend/` (all gitignored, embedded by the csproj), then publishes the single-file self-contained `speedseat.exe` with `-p:Version`. Release assets: `speedseat.exe` and `firmware.bin`.

Backend, frontend and firmware must always be released together — the update chain (GitHub release check → exe download → firmware OTA on next connect) assumes their versions match.

`publish_release.ps1` (legacy) just bumps and pushes a tag; the workflow no longer triggers on tags.

---

## Keep this file updated

Update this file when:
- New SignalR hubs or command IDs are added
- Motor wiring / axis mapping changes
- New compile-time flags are added to `configuration.h`
- The serial protocol changes
- New major frontend views or settings are added
- The release/build process changes
