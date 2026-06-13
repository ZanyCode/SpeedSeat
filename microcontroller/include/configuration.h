#ifndef CONFIG_H
#define CONFIG_H
#define LED_BUILTIN 2
#define STEP_SETTING 800
#define ACTUAL_STEPS_PER_ROTATION (STEP_SETTING * 2)
//Spindel 4mm hub/Undrehung | Übersetzung Zahnräder 4/1
#define MILLIMETER_PER_ROTATION (4*4) 
#define STEPS_PER_MM (ACTUAL_STEPS_PER_ROTATION / MILLIMETER_PER_ROTATION)

//#define NO_HARDWARE

//#define ANALYZE_MOTION_CERNEL

#define USE_EEPROM

//Autmatically safe parameters to eeprom of microcontroller. If not defined a safe command has to be send to safe everything.
//#define AUTO_SAVE

#define AUTO_RETURN_TO_ZERO

//define to spit out Serial information about state of the loop. Usefull when micocontroller crashes.
//#define DEBUG

//Numeric firmware version reported to the PC for the OTA update handshake.
//CI overrides this via build flag (-DFW_VERSION_NUMBER=<release build number>); 0 = local dev build.
#ifndef FW_VERSION_NUMBER
#define FW_VERSION_NUMBER 0
#endif

//Communication with the PC is always over WiFi/UDP (never USB-serial). WiFi credentials are
//not hard-coded: on first boot (or when the saved network is unreachable) the ESP opens a
//"SpeedSeat-Setup" access point with a captive configuration portal (tzapu WiFiManager).
//The PC then finds the ESP via UDP broadcast discovery.
//Must match SpeedseatUdpProtocol.Port in backend/Processing/UdpConnection.cs
#define UDP_PORT 8888







//Automatic definitions
#ifdef NO_HARDWARE
#define IGNORE_ENDSTOP
#define IGNORE_DRIVE_ERRORS
#define ALLOW_MOVEMENT_AFTER_BOOTUP
#else
#define HOMING_REQUIRED
#endif

#ifdef DEBUG
    #undef DEBUG
    #define DEBUG(x) printPosition(x);
#else
    #define DEBUG(x)
#endif
#include "pins.h"
#endif

#ifdef NO_HARDWARE
#define MOVEMENT_ALLOWED (true)
#else
#define MOVEMENT_ALLOWED (!Axis::digitalReadAverage(PIN_ENABLE))
#endif
#define MOVEMENT_NOT_ALLOWED (!MOVEMENT_ALLOWED)