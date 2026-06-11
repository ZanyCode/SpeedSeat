#include "ota.h"
#include <HTTPClient.h>
#include <Update.h>

void performOtaUpdate(IPAddress host, uint16_t port)
{
    String url = "http://" + host.toString() + ":" + String(port) + "/firmware.bin";
    Serial.printf("OTA: downloading %s\n", url.c_str());

    HTTPClient http;
    if (!http.begin(url))
    {
        Serial.println("OTA: could not start HTTP connection");
        return;
    }

    int httpCode = http.GET();
    if (httpCode != HTTP_CODE_OK)
    {
        Serial.printf("OTA: HTTP error %d\n", httpCode);
        http.end();
        return;
    }

    int contentLength = http.getSize();
    if (contentLength <= 0)
    {
        Serial.println("OTA: invalid content length");
        http.end();
        return;
    }

    if (!Update.begin(contentLength))
    {
        Serial.printf("OTA: not enough space (%s)\n", Update.errorString());
        http.end();
        return;
    }

    Serial.printf("OTA: flashing %d bytes...\n", contentLength);
    size_t written = Update.writeStream(http.getStream());
    http.end();

    if (written != (size_t)contentLength || !Update.end(true))
    {
        Serial.printf("OTA: update failed after %u bytes (%s)\n", written, Update.errorString());
        Update.abort();
        return;
    }

    Serial.println("OTA: success, restarting with new firmware");
    delay(200);
    ESP.restart();
}
