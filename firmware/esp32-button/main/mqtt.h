#ifndef SAFESIGNAL_MQTT_H
#define SAFESIGNAL_MQTT_H

#include <stdbool.h>
#include "esp_err.h"

/**
 * Initialize MQTT client and connect to broker
 */
void mqtt_init(void);

/**
 * Publish alert to MQTT broker
 * @return true if published successfully, false otherwise
 */
bool mqtt_publish_alert(void);

/**
 * Publish device status to MQTT broker
 * @return true if published successfully, false otherwise
 */
bool mqtt_publish_status(void);

/**
 * Publish heartbeat to MQTT broker
 * @return true if published successfully, false otherwise
 */
bool mqtt_publish_heartbeat(void);

/**
 * Check if MQTT client is connected
 * @return true if connected, false otherwise
 */
bool mqtt_is_connected(void);

#endif /* SAFESIGNAL_MQTT_H */
