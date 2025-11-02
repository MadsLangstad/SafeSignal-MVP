#ifndef SAFESIGNAL_WIFI_H
#define SAFESIGNAL_WIFI_H

#include "esp_err.h"

/**
 * Initialize WiFi subsystem and connect to configured network
 */
void wifi_init(void);

/**
 * Get WiFi connection status
 * @return true if connected, false otherwise
 */
bool wifi_is_connected(void);

/**
 * Get WiFi RSSI (signal strength)
 * @return RSSI in dBm
 */
int8_t wifi_get_rssi(void);

#endif /* SAFESIGNAL_WIFI_H */
