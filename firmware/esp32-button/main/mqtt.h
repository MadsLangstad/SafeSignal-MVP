#ifndef SAFESIGNAL_MQTT_H
#define SAFESIGNAL_MQTT_H

#include <stdbool.h>
#include "esp_err.h"
#include "alert_queue.h"

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
 * Publish alert from queue (used by alert_queue.c)
 * @param alert Queued alert data
 * @return true if published successfully, false otherwise
 */
bool mqtt_publish_alert_from_queue(const queued_alert_t *alert);

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
