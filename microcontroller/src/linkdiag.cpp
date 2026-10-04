#include "configuration.h"
#ifdef LINK_DIAGNOSTICS
#include "linkdiag.h"
#include "Arduino.h"
#include <WiFi.h>
#include "ping/ping_sock.h"

// Replies slower than this are printed; faster ones are only counted.
static const uint32_t SLOW_PING_MS = 25;
static const uint32_t PING_INTERVAL_MS = 40;
static const uint32_t SUMMARY_INTERVAL_MS = 10000;

static uint32_t pingCount = 0;
static uint32_t slowCount = 0;
static uint32_t lostCount = 0;
static uint32_t lastSummary = 0;

static void printSummaryIfDue()
{
    if (millis() - lastSummary < SUMMARY_INTERVAL_MS)
    {
        return;
    }
    lastSummary = millis();
    Serial.printf("DIAG t=%lu pings=%u slow=%u lost=%u rssi=%d\n", millis(), pingCount, slowCount, lostCount, WiFi.RSSI());
    pingCount = slowCount = lostCount = 0;
}

static void onPingSuccess(esp_ping_handle_t handle, void *args)
{
    uint32_t elapsed;
    esp_ping_get_profile(handle, ESP_PING_PROF_TIMEGAP, &elapsed, sizeof(elapsed));
    pingCount++;
    if (elapsed >= SLOW_PING_MS)
    {
        slowCount++;
        Serial.printf("DIAG t=%lu gwping=%ums\n", millis(), elapsed);
    }
    printSummaryIfDue();
}

static void onPingTimeout(esp_ping_handle_t handle, void *args)
{
    pingCount++;
    lostCount++;
    Serial.printf("DIAG t=%lu gwping=LOST\n", millis());
    printSummaryIfDue();
}

void startLinkDiagnostics()
{
    WiFi.onEvent([](WiFiEvent_t event, WiFiEventInfo_t info) {
        Serial.printf("DIAG t=%lu wifi disconnected, reason=%d\n", millis(), info.wifi_sta_disconnected.reason);
    }, ARDUINO_EVENT_WIFI_STA_DISCONNECTED);

    esp_ping_config_t config = ESP_PING_DEFAULT_CONFIG();
    IPAddress gateway = WiFi.gatewayIP();
    config.target_addr.type = IPADDR_TYPE_V4;
    config.target_addr.u_addr.ip4.addr = (uint32_t)gateway;
    config.count = ESP_PING_COUNT_INFINITE;
    config.interval_ms = PING_INTERVAL_MS;
    config.timeout_ms = 1000;

    esp_ping_callbacks_t callbacks = {};
    callbacks.on_ping_success = onPingSuccess;
    callbacks.on_ping_timeout = onPingTimeout;

    esp_ping_handle_t handle;
    if (esp_ping_new_session(&config, &callbacks, &handle) == ESP_OK)
    {
        esp_ping_start(handle);
        Serial.printf("DIAG link diagnostics started, pinging gateway %s every %ums, channel %d, rssi %d\n",
                      gateway.toString().c_str(), PING_INTERVAL_MS, WiFi.channel(), WiFi.RSSI());
    }
    else
    {
        Serial.println("DIAG could not start ping session");
    }
}
#endif
