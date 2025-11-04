#ifndef SAFESIGNAL_CONFIG_H
#define SAFESIGNAL_CONFIG_H

#include <stdint.h>
#include <stdbool.h>

/* ========================================================================== */
/* SafeSignal ESP32-S3 Button Configuration                                   */
/* ========================================================================== */

/* Version */
#define SAFESIGNAL_VERSION "1.0.0-alpha"

/* Hardware Configuration */
/* ========================================================================== */

/* Button GPIO */
#define BUTTON_PIN 0
#define BUTTON_ACTIVE_LOW true
#define BUTTON_DEBOUNCE_MS 50

/* LED GPIO */
#define LED_PIN 2
#define LED_ACTIVE_HIGH true

/* I2C for ATECC608A */
#define I2C_SDA_PIN 21
#define I2C_SCL_PIN 22
#define I2C_FREQUENCY 100000

/* Network Configuration */
/* ========================================================================== */

/* WiFi */
#define WIFI_SSID "SafeSignal-Edge"
#define WIFI_PASS "safesignal-dev"
#define WIFI_CONNECT_TIMEOUT_MS 20000
#define WIFI_RECONNECT_INTERVAL_MS 5000

/* MQTT Broker */
#define MQTT_BROKER_URI "mqtts://edge-gateway.local:8883"
#define MQTT_KEEPALIVE_SECONDS 30
#define MQTT_QOS 1
#define MQTT_RECONNECT_INTERVAL_MS 5000

/* Device Configuration */
/* ========================================================================== */

#define DEVICE_ID "esp32-dev-001"
#define TENANT_ID "tenant-a"
#define BUILDING_ID "building-a"
#define ROOM_ID "room-1"

/* Alert Modes */
typedef enum {
    ALERT_MODE_SILENT = 0,
    ALERT_MODE_AUDIBLE = 1,
    ALERT_MODE_LOCKDOWN = 2,
    ALERT_MODE_EVACUATION = 3
} alert_mode_t;

#define DEFAULT_ALERT_MODE ALERT_MODE_AUDIBLE

/* Timing & Performance */
/* ========================================================================== */

#define STATUS_REPORT_INTERVAL_MS 60000
#define HEARTBEAT_INTERVAL_MS 30000
#define ALERT_TRIGGER_TIMEOUT_MS 100
#define MQTT_PUBLISH_TIMEOUT_MS 5000

/* Rate Limiting */
/* ========================================================================== */

/* Firmware-level rate limiting (DoS protection) */
#define RATE_LIMIT_ENABLED true
#define RATE_LIMIT_MAX_ALERTS 10           /* Max alerts in time window */
#define RATE_LIMIT_WINDOW_SECONDS 60       /* Time window in seconds */
#define RATE_LIMIT_COOLDOWN_SECONDS 300    /* Cooldown after exceeding limit (5 minutes) */

/* Individual alert throttling (debounce for accidental presses) */
#define ALERT_MIN_INTERVAL_MS 2000          /* Minimum 2s between alerts */

/* Buffer Sizes */
/* ========================================================================== */

#define JSON_BUFFER_SIZE 512
#define TOPIC_BUFFER_SIZE 128
#define PAYLOAD_BUFFER_SIZE 384

#endif /* SAFESIGNAL_CONFIG_H */
