#ifndef SAFESIGNAL_ALERT_QUEUE_H
#define SAFESIGNAL_ALERT_QUEUE_H

#include <stdint.h>
#include <stdbool.h>
#include "esp_err.h"

/**
 * Alert Queue - NVS-based persistent storage for reliable alert delivery
 *
 * Ensures alerts are never lost due to network failures by:
 * - Storing alerts in NVS before MQTT publish attempt
 * - Retrying failed alerts on reconnection
 * - Implementing retry limits and expiration
 */

#define ALERT_QUEUE_MAX_SIZE 50
#define ALERT_QUEUE_MAX_RETRIES 10
#define ALERT_QUEUE_EXPIRY_SECONDS 3600  /* 1 hour */

typedef struct {
    uint32_t alert_id;          /* Unique alert identifier */
    uint32_t timestamp;         /* UTC timestamp (seconds since epoch) */
    uint32_t retry_count;       /* Number of retry attempts */
    uint32_t created_at;        /* Boot time when alert was created */
    char device_id[32];
    char tenant_id[32];
    char building_id[32];
    char room_id[32];
    uint8_t mode;               /* Alert mode (SILENT, AUDIBLE, etc.) */
    char version[16];
} queued_alert_t;

/**
 * Initialize alert queue system
 * - Opens NVS namespace
 * - Loads any pending alerts from previous session
 * @return ESP_OK on success
 */
esp_err_t alert_queue_init(void);

/**
 * Enqueue a new alert for delivery
 * @param alert Alert data to persist
 * @return ESP_OK on success, ESP_ERR_NO_MEM if queue full
 */
esp_err_t alert_queue_enqueue(const queued_alert_t *alert);

/**
 * Attempt to deliver all pending alerts
 * @return Number of alerts successfully delivered
 */
int alert_queue_process(void);

/**
 * Get count of pending alerts in queue
 * @return Number of alerts waiting for delivery
 */
int alert_queue_get_count(void);

/**
 * Clear expired alerts from queue
 * @return Number of alerts removed
 */
int alert_queue_cleanup_expired(void);

/**
 * Get queue statistics
 */
typedef struct {
    uint32_t total_enqueued;
    uint32_t total_delivered;
    uint32_t total_expired;
    uint32_t total_failed;
    uint32_t pending_count;
} alert_queue_stats_t;

esp_err_t alert_queue_get_stats(alert_queue_stats_t *stats);

#endif /* SAFESIGNAL_ALERT_QUEUE_H */
