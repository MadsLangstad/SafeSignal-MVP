/**
 * SafeSignal Rate Limiting Implementation
 *
 * Sliding window algorithm with cooldown period for DoS protection.
 */

#include "rate_limit.h"
#include "config.h"

#include <string.h>
#include <time.h>
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "esp_log.h"

static const char *TAG = "RATE_LIMIT";

/* Sliding window data structure */
typedef struct {
    uint32_t timestamps[RATE_LIMIT_MAX_ALERTS];  /* Alert timestamps (seconds since boot) */
    uint32_t count;                               /* Number of alerts in window */
    uint32_t window_start;                        /* Window start time (seconds) */
    uint32_t cooldown_until;                      /* Cooldown end time (0 if not in cooldown) */
    uint32_t last_alert_time_ms;                  /* Last alert time in milliseconds (for min interval) */
} rate_limit_state_t;

static rate_limit_state_t state = {0};
static SemaphoreHandle_t state_mutex = NULL;

esp_err_t rate_limit_init(void)
{
    /* Create mutex for thread-safe access */
    state_mutex = xSemaphoreCreateMutex();
    if (state_mutex == NULL) {
        ESP_LOGE(TAG, "Failed to create mutex");
        return ESP_ERR_NO_MEM;
    }

    /* Initialize state */
    memset(&state, 0, sizeof(state));
    state.window_start = 0;
    state.cooldown_until = 0;

    ESP_LOGI(TAG, "Rate limiting initialized:");
    ESP_LOGI(TAG, "  Max alerts: %d per %d seconds",
             RATE_LIMIT_MAX_ALERTS, RATE_LIMIT_WINDOW_SECONDS);
    ESP_LOGI(TAG, "  Cooldown: %d seconds", RATE_LIMIT_COOLDOWN_SECONDS);
    ESP_LOGI(TAG, "  Min interval: %d ms", ALERT_MIN_INTERVAL_MS);

    return ESP_OK;
}

bool rate_limit_check_alert(void)
{
#if !RATE_LIMIT_ENABLED
    /* Rate limiting disabled in config */
    return true;
#endif

    if (state_mutex == NULL) {
        ESP_LOGE(TAG, "Rate limiting not initialized");
        return false;
    }

    xSemaphoreTake(state_mutex, portMAX_DELAY);

    /* Get current time in seconds (uptime) */
    uint32_t now_seconds = xTaskGetTickCount() * portTICK_PERIOD_MS / 1000;

    /* Check if in cooldown period */
    if (state.cooldown_until > 0) {
        if (now_seconds < state.cooldown_until) {
            uint32_t remaining = state.cooldown_until - now_seconds;
            ESP_LOGW(TAG, "⚠️  RATE LIMITED: In cooldown period (%lu seconds remaining)",
                     remaining);
            xSemaphoreGive(state_mutex);
            return false;
        } else {
            /* Cooldown expired, reset */
            ESP_LOGI(TAG, "Cooldown period expired, resetting rate limit");
            state.cooldown_until = 0;
            state.count = 0;
            state.window_start = now_seconds;
        }
    }

    /* Initialize window if first alert */
    if (state.window_start == 0) {
        state.window_start = now_seconds;
        state.count = 0;
    }

    /* Check if window has expired (sliding window) */
    uint32_t window_age = now_seconds - state.window_start;
    if (window_age >= RATE_LIMIT_WINDOW_SECONDS) {
        /* Start new window */
        ESP_LOGD(TAG, "Rate limit window expired, starting new window");
        state.window_start = now_seconds;
        state.count = 0;
    } else {
        /* Within window, check count */
        if (state.count >= RATE_LIMIT_MAX_ALERTS) {
            /* Limit exceeded, enter cooldown */
            state.cooldown_until = now_seconds + RATE_LIMIT_COOLDOWN_SECONDS;
            ESP_LOGW(TAG, "");
            ESP_LOGW(TAG, "╔═══════════════════════════════════════════════════════════╗");
            ESP_LOGW(TAG, "║   ⚠️  RATE LIMIT EXCEEDED - COOLDOWN ACTIVATED           ║");
            ESP_LOGW(TAG, "╚═══════════════════════════════════════════════════════════╝");
            ESP_LOGW(TAG, "Alerts: %lu in %lu seconds (limit: %d per %d seconds)",
                     state.count, window_age, RATE_LIMIT_MAX_ALERTS, RATE_LIMIT_WINDOW_SECONDS);
            ESP_LOGW(TAG, "Cooldown: %d seconds", RATE_LIMIT_COOLDOWN_SECONDS);
            ESP_LOGW(TAG, "");
            xSemaphoreGive(state_mutex);
            return false;
        }
    }

    /* Alert allowed */
    xSemaphoreGive(state_mutex);
    return true;
}

void rate_limit_record_alert(void)
{
    if (state_mutex == NULL) {
        return;
    }

    xSemaphoreTake(state_mutex, portMAX_DELAY);

    uint32_t now_seconds = xTaskGetTickCount() * portTICK_PERIOD_MS / 1000;

    /* Record timestamp in circular buffer */
    if (state.count < RATE_LIMIT_MAX_ALERTS) {
        state.timestamps[state.count] = now_seconds;
        state.count++;
    }

    ESP_LOGD(TAG, "Alert recorded: %lu/%d in window (window age: %lu seconds)",
             state.count, RATE_LIMIT_MAX_ALERTS,
             now_seconds - state.window_start);

    xSemaphoreGive(state_mutex);
}

bool rate_limit_check_min_interval(void)
{
    if (state_mutex == NULL) {
        return true;
    }

    xSemaphoreTake(state_mutex, portMAX_DELAY);

    uint32_t now_ms = xTaskGetTickCount() * portTICK_PERIOD_MS;

    /* Check minimum interval */
    if (state.last_alert_time_ms > 0) {
        uint32_t elapsed = now_ms - state.last_alert_time_ms;
        if (elapsed < ALERT_MIN_INTERVAL_MS) {
            uint32_t remaining = ALERT_MIN_INTERVAL_MS - elapsed;
            ESP_LOGD(TAG, "Alert throttled: %lu ms since last alert (min: %d ms, remaining: %lu ms)",
                     elapsed, ALERT_MIN_INTERVAL_MS, remaining);
            xSemaphoreGive(state_mutex);
            return false;
        }
    }

    /* Update last alert time */
    state.last_alert_time_ms = now_ms;

    xSemaphoreGive(state_mutex);
    return true;
}

esp_err_t rate_limit_get_status(uint32_t *alerts_sent,
                                 uint32_t *window_start_time,
                                 uint32_t *cooldown_until)
{
    if (state_mutex == NULL) {
        return ESP_ERR_INVALID_STATE;
    }

    if (alerts_sent == NULL || window_start_time == NULL || cooldown_until == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    xSemaphoreTake(state_mutex, portMAX_DELAY);

    *alerts_sent = state.count;
    *window_start_time = state.window_start;
    *cooldown_until = state.cooldown_until;

    xSemaphoreGive(state_mutex);

    return ESP_OK;
}

void rate_limit_reset(void)
{
    if (state_mutex == NULL) {
        return;
    }

    xSemaphoreTake(state_mutex, portMAX_DELAY);

    memset(&state, 0, sizeof(state));
    state.window_start = 0;
    state.cooldown_until = 0;
    state.last_alert_time_ms = 0;

    ESP_LOGI(TAG, "Rate limit state reset");

    xSemaphoreGive(state_mutex);
}
