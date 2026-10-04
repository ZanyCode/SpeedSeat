#ifndef LINKDIAG_H
#define LINKDIAG_H

// WiFi link diagnostics (only compiled with LINK_DIAGNOSTICS, see configuration.h).
// Pings the WiFi gateway from the ESP in a background task and prints slow/lost replies,
// RSSI and WiFi disconnects to the USB serial log. Lets a latency problem be attributed
// to the ESP<->router hop independently of the PC. Call once after WiFi is connected.
void startLinkDiagnostics();

#endif
