#include "transport.h"
#include <WiFi.h>

// Must match SpeedseatUdpProtocol in backend/Processing/UdpConnection.cs
static const char *DISCOVERY_REQUEST = "SPEEDSEAT_DISCOVERY";
static const char *DISCOVERY_RESPONSE = "SPEEDSEAT_ESP32";

void UdpTransport::begin(const char *ssid, const char *password, uint16_t port)
{
    WiFi.mode(WIFI_STA);
    WiFi.begin(ssid, password);
    Serial.print("Connecting to WiFi");
    while (WiFi.status() != WL_CONNECTED)
    {
        delay(250);
        Serial.print(".");
    }
    // Modem power save adds 100+ ms latency spikes to UDP — disable it.
    WiFi.setSleep(false);
    Serial.printf("\nWiFi connected, IP: %s\n", WiFi.localIP().toString().c_str());

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
