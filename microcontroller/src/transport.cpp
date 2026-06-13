#include "transport.h"
#include <WiFi.h>
#include <WiFiManager.h>

// Must match SpeedseatUdpProtocol in backend/Processing/UdpConnection.cs
static const char *DISCOVERY_REQUEST = "SPEEDSEAT_DISCOVERY";
static const char *DISCOVERY_RESPONSE = "SPEEDSEAT_ESP32";

void UdpTransport::begin(uint16_t port)
{
    WiFi.mode(WIFI_STA);

    // No hard-coded credentials: WiFiManager tries the last saved network and, if that
    // fails (or none is stored), opens the "SpeedSeat-Setup" access point with a captive
    // portal so the user can pick a network from their phone/PC. Blocks until connected.
    WiFiManager wm;
    wm.setConfigPortalTimeout(0); // stay in the portal until WiFi is configured
    Serial.println("Connecting to WiFi (opens 'SpeedSeat-Setup' portal if not yet configured)...");
    if (!wm.autoConnect("SpeedSeat-Setup"))
    {
        Serial.println("WiFi connect/portal failed — restarting");
        delay(1000);
        ESP.restart();
    }

    // Modem power save adds 100+ ms latency spikes to UDP — disable it. Auto-reconnect keeps
    // the saved network alive after a router/WiFi hiccup without reopening the portal.
    WiFi.setSleep(false);
    WiFi.setAutoReconnect(true);
    Serial.printf("WiFi connected, IP: %s\n", WiFi.localIP().toString().c_str());

    if (udp.listen(port))
    {
        Serial.printf("Listening on UDP port %d\n", port);
        udp.onPacket([this](AsyncUDPPacket packet) {
            // Discovery packets get answered immediately and are not part of the protocol stream
            if (packet.length() == strlen(DISCOVERY_REQUEST) &&
                memcmp(packet.data(), DISCOVERY_REQUEST, packet.length()) == 0)
            {
                packet.write((const uint8_t *)DISCOVERY_RESPONSE, strlen(DISCOVERY_RESPONSE));
                return;
            }

            // Protocol data: remember the sender so responses/acks reach the right PC
            peerIp = packet.remoteIP();
            peerPort = packet.remotePort();
            hasPeer = true;

            for (size_t i = 0; i < packet.length(); i++)
            {
                size_t next = (rxHead + 1) % RX_BUFFER_SIZE;
                if (next == rxTail)
                {
                    return; // buffer full, drop the rest; protocol timeout/hash check recovers
                }
                rxBuffer[rxHead] = packet.data()[i];
                rxHead = next;
            }
        });
    }
}

int UdpTransport::available()
{
    return (rxHead + RX_BUFFER_SIZE - rxTail) % RX_BUFFER_SIZE;
}

int UdpTransport::read()
{
    if (rxHead == rxTail)
    {
        return -1;
    }
    uint8_t value = rxBuffer[rxTail];
    rxTail = (rxTail + 1) % RX_BUFFER_SIZE;
    return value;
}

void UdpTransport::write(const uint8_t *data, size_t length)
{
    for (size_t i = 0; i < length && txLength < TX_BUFFER_SIZE; i++)
    {
        txBuffer[txLength++] = data[i];
    }
}

void UdpTransport::flush()
{
    if (txLength == 0)
    {
        return;
    }
    if (hasPeer)
    {
        udp.writeTo(txBuffer, txLength, peerIp, peerPort);
    }
    txLength = 0;
}
