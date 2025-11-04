/**
 * SafeSignal Rate Limiting Module
 *
 * Firmware-level rate limiting to prevent DoS attacks and accidental spamming.
 * Implements sliding window algorithm with cooldown period.
 */

#ifndef SAFESIGNAL_RATE_LIMIT_H
#define SAFESIGNAL_RATE_LIMIT_H

#include <stdint.h>
#include <stdbool.h>
#include "esp_err.h"

/**
 * @brief Initialize rate limiting system
 *
 * Must be called before using rate limit functions.
 *
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_NO_MEM if insufficient memory
 */
esp_err_t rate_limit_init(void);

/**
 * @brief Check if alert can be sent (rate limit check)
 *
 * Implements sliding window rate limiting:
 * - Tracks alerts in time window
 * - Enforces maximum alerts per window
 * - Applies cooldown period if limit exceeded
 *
 * @return
 *  - true if alert allowed
 *  - false if rate limited (log warning with reason)
 */
bool rate_limit_check_alert(void);

/**
 * @brief Record an alert send attempt
 *
 * Call this after successfully sending an alert.
 * Updates the sliding window with current timestamp.
 */
void rate_limit_record_alert(void);

/**
 * @brief Get current rate limit status
 *
 * @param alerts_sent Number of alerts sent in current window (output)
 * @param window_start_time Start time of current window in seconds (output)
 * @param cooldown_until Cooldown end time in seconds, 0 if not in cooldown (output)
 *
 * @return ESP_OK on success
 */
esp_err_t rate_limit_get_status(uint32_t *alerts_sent,
                                 uint32_t *window_start_time,
                                 uint32_t *cooldown_until);

/**
 * @brief Reset rate limiting state (for testing/debugging)
 *
 * Clears alert history and cooldown state.
 */
void rate_limit_reset(void);

/**
 * @brief Check minimum interval between alerts
 *
 * Separate from sliding window, enforces minimum time between individual alerts
 * to prevent accidental double-presses.
 *
 * @return
 *  - true if enough time has passed since last alert
 *  - false if too soon (throttled)
 */
bool rate_limit_check_min_interval(void);

#endif /* SAFESIGNAL_RATE_LIMIT_H */
