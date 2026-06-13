#ifndef TRANSPORT_H
#define TRANSPORT_H

#include "Arduino.h"
#include <AsyncUDP.h>

// Byte-stream abstraction so the communication class can run the 8-byte protocol
// over WiFi/UDP. (USB-serial is no longer a transport — the seat talks to the PC over WiFi.)
class Transport
{
public:
    virtual int available() = 0;
    virtual int read() = 0; // returns -1 if no byte is available
    virtual void write(const uint8_t *data, size_t length) = 0;
    virtual void flush() = 0;
};

// UDP transport. The PC discovers the ESP via a broadcast magic packet, then sends
// protocol bytes as datagrams. Replies go to the endpoint of the last received
// protocol datagram. write() collects bytes, flush() sends them as one datagram.
class UdpTransport : public Transport
{
public:
    // Connects to WiFi via the WiFiManager captive portal (blocking) and starts
    // listening. Call once in setup().
    void begin(uint16_t port);

    int available() override;
    int read() override;
    void write(const uint8_t *data, size_t length) override;
    void flush() override;

    // IP of the PC the protocol is currently talking to (needed for OTA downloads).
    IPAddress getPeerIp() { return peerIp; }
    bool hasPeerEndpoint() { return hasPeer; }

private:
    static const size_t RX_BUFFER_SIZE = 1024;
    static const size_t TX_BUFFER_SIZE = 64;

    AsyncUDP udp;

    // Ring buffer filled from the AsyncUDP callback (runs in the LwIP task) and
    // drained from loop() — single producer / single consumer, so volatile
    // indices are sufficient.
    uint8_t rxBuffer[RX_BUFFER_SIZE];
    volatile size_t rxHead = 0;
    volatile size_t rxTail = 0;

    uint8_t txBuffer[TX_BUFFER_SIZE];
    size_t txLength = 0;

    IPAddress peerIp;
    uint16_t peerPort = 0;
    volatile bool hasPeer = false;
};

#endif
