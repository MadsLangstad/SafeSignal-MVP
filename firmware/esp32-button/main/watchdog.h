#ifndef SAFESIGNAL_WATCHDOG_H
#define SAFESIGNAL_WATCHDOG_H

#include "esp_err.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

/**
 * Watchdog Configuration
 *
 * Provides task watchdog and interrupt watchdog monitoring to:
 * - Detect task hangs and deadlocks
 * - Recover from interrupt handler failures
 * - Auto-reboot on critical failures
 */

#define WATCHDOG_TIMEOUT_SECONDS 30

/**
 * Initialize watchdog subsystem
 * - Enables task watchdog timer (TWDT)
 * - Configures panic behavior on timeout
 * @return ESP_OK on success
 */
esp_err_t watchdog_init(void);

/**
 * Register a task for watchdog monitoring
 * @param task_handle FreeRTOS task handle to monitor
 * @param task_name Task name for logging
 * @return ESP_OK on success
 */
esp_err_t watchdog_add_task(TaskHandle_t task_handle, const char *task_name);

/**
 * Feed the watchdog for current task
 * Tasks must call this periodically (< timeout interval)
 * @return ESP_OK on success
 */
esp_err_t watchdog_feed(void);

/**
 * Remove task from watchdog monitoring
 * @param task_handle Task to remove
 * @return ESP_OK on success
 */
esp_err_t watchdog_remove_task(TaskHandle_t task_handle);

#endif /* SAFESIGNAL_WATCHDOG_H */
