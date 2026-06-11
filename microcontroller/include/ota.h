#ifndef OTA_H
#define OTA_H

#include "Arduino.h"

// Downloads /firmware.bin from the given host (the PC running the SpeedSeat backend),
// flashes it and restarts the ESP. Blocking; only returns on failure.
// Requires an active WiFi connection (USE_UDP builds).
void performOtaUpdate(IPAddress host, uint16_t port);

#endif
