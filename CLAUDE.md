# SpeedSeat — Project Guide for Claude

## What this project is

SpeedSeat is a motion-simulator racing seat controller. It drives three stepper-motor axes (FrontLeft, FrontRight, Back) to tilt a seat in real time based on F1 2020 game telemetry or manual input. The system has three parts:

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
| `Processing/CommandService.cs` | Serial port management, 8-byte protocol read/write, ACK handling |
| `Processing/F12020TelemetryAdaptor.cs` | Receives F1 2020 UDP telemetry (port 20777), maps G-forces to tilt |
| `Processing/OutdatedDataDiscardQueue.cs` | Drop-last-value queue to prevent stale motor position commands |
| `Data/SpeedseatSettings.cs` | All user-configurable settings stored in SQLite; exposes `IObservable<T>` for reactive updates |
| `Data/SpeedseatContext.cs` | EF Core SQLite context (`speedseat_dbversion2.sqlite3`) |
| `Data/Config.cs` | Typed representation of `config.json` |
| `Api/*.cs` | SignalR hubs: `ManualControlHub`, `ConnectionHub`, `InfoHub`, `ProgramSettingsHub`, `SeatSettingsHub`, `TelemetryHub` |
| `config.json` | Command definitions (IDs, value ranges, labels). Auto-created from embedded `config_template.json` if missing. |

**DI singletons**: `Speedseat`, `CommandService`, `OutdatedDataDiscardQueue<Command>`, `F12020TelemetryAdaptor`, `SpeedseatSettings`, `FrontendLogger`.

### Frontend (`frontend/src/app/`)

Angular SPA with Angular Material UI. Main views:
- **ManualControl** — direct slider control of motor positions
- **SeatSettings** — per-command settings read from `config.json` (numeric/boolean/action widgets)
- **ProgramSettings** — response curve editor, telemetry multipliers/caps, motor index mapping
- **Telemetry** — live Plotly chart of front/side tilt from F1 2020

All backend communication is over SignalR (not REST). Hub URLs: `/hub/manual`, `/hub/connection`, `/hub/info`, `/hub/programSettings`, `/hub/seatSettings`, `/hub/telemetry`.

### Microcontroller (`microcontroller/`)

Target: **AZ-Delivery DevKit v4 (ESP32)**. Built with PlatformIO.

- `src/main.cpp` — setup/loop, dispatches commands to X/Y/Z Axis objects
- `src/Axis.cpp` + `include/Axis*.h` — stepper axis: homing, movement, EEPROM load/save
- `src/communication.cpp` + `include/communication.h` — 8-byte protocol implementation
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

## Serial Communication Protocol

8-byte fixed-length packets over **38400 baud** serial.

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
| 0x42 | PC→MC | Reset EEPROM (can be sent without full connection) |

**Connection sequence**: PC sends 0x01 → MC performs sync (read/write requests) → MC sends 0x02 → UI unblocks.

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

---

## Build & release

`publish_release.ps1` — builds frontend (`npm run build`), copies output to `backend/wwwroot/`, then publishes the .NET project as a single-file self-contained executable. GitHub Actions workflow in `.github/` handles CI builds.

---

## Keep this file updated

Update this file when:
- New SignalR hubs or command IDs are added
- Motor wiring / axis mapping changes
- New compile-time flags are added to `configuration.h`
- The serial protocol changes
- New major frontend views or settings are added
- The release/build process changes
