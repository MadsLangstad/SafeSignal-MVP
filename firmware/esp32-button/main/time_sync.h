#ifndef SAFESIGNAL_TIME_SYNC_H
#define SAFESIGNAL_TIME_SYNC_H

#include <time.h>
#include <stdbool.h>
#include "esp_err.h"

/**
 * Time Synchronization via SNTP/NTP
 *
 * Provides UTC time synchronization for accurate alert timestamps:
 * - Syncs with NTP servers on WiFi connection
 * - Maintains time accuracy across reboots (if RTC battery present)
 * - Provides callbacks for sync events
 */

/**
 * Initialize time synchronization
 * - Configures SNTP client
 * - Sets timezone to UTC
 * - Starts sync process when WiFi connected
 * @return ESP_OK on success
 */
esp_err_t time_sync_init(void);

/**
 * Check if time has been synchronized
 * @return true if time is synchronized, false otherwise
 */
bool time_is_synchronized(void);

/**
 * Get current UTC timestamp
 * @param out_time Pointer to store current time
 * @return ESP_OK if time is valid, ESP_ERR_INVALID_STATE if not synced
 */
esp_err_t time_get_utc(time_t *out_time);

/**
 * Get human-readable time string
 * @param buffer Output buffer
 * @param buf_size Buffer size
 * @return ESP_OK on success
 */
esp_err_t time_get_string(char *buffer, size_t buf_size);

/**
 * Wait for time synchronization (blocking)
 * @param timeout_ms Timeout in milliseconds (0 = no timeout)
 * @return ESP_OK if synchronized, ESP_ERR_TIMEOUT on timeout
 */
esp_err_t time_wait_for_sync(uint32_t timeout_ms);

#endif /* SAFESIGNAL_TIME_SYNC_H */
