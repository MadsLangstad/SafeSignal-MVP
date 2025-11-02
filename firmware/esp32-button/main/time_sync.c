#include "time_sync.h"
#include "config.h"

#include <string.h>
#include <sys/time.h>
#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"
#include "esp_log.h"
#include "esp_sntp.h"

static const char *TAG = "TIME_SYNC";

/* NTP servers (prioritize pool.ntp.org for reliability) */
#define NTP_SERVER_PRIMARY   "pool.ntp.org"
#define NTP_SERVER_SECONDARY "time.google.com"
#define NTP_SERVER_TERTIARY  "time.cloudflare.com"

static bool initialized = false;
static bool synchronized = false;
static EventGroupHandle_t time_event_group = NULL;
static const int TIME_SYNCED_BIT = BIT0;

/* SNTP sync notification callback */
static void time_sync_notification_cb(struct timeval *tv)
{
    time_t now = tv->tv_sec;
    struct tm timeinfo;
    gmtime_r(&now, &timeinfo);

    char strftime_buf[64];
    strftime(strftime_buf, sizeof(strftime_buf), "%Y-%m-%d %H:%M:%S", &timeinfo);

    ESP_LOGI(TAG, "[TIME] Synchronized: %s UTC", strftime_buf);

    synchronized = true;

    if (time_event_group != NULL) {
        xEventGroupSetBits(time_event_group, TIME_SYNCED_BIT);
    }
}

esp_err_t time_sync_init(void)
{
    if (initialized) {
        ESP_LOGW(TAG, "[TIME] Already initialized");
        return ESP_OK;
    }

    /* Create event group for sync notification */
    time_event_group = xEventGroupCreate();
    if (time_event_group == NULL) {
        ESP_LOGE(TAG, "[TIME] Failed to create event group");
        return ESP_ERR_NO_MEM;
    }

    /* Set timezone to UTC */
    setenv("TZ", "UTC0", 1);
    tzset();

    /* Configure SNTP */
    esp_sntp_setoperatingmode(SNTP_OPMODE_POLL);
    esp_sntp_setservername(0, NTP_SERVER_PRIMARY);
    esp_sntp_setservername(1, NTP_SERVER_SECONDARY);
    esp_sntp_setservername(2, NTP_SERVER_TERTIARY);

    /* Set sync notification callback */
    sntp_set_time_sync_notification_cb(time_sync_notification_cb);

    /* Set sync mode to immediate update */
    sntp_set_sync_mode(SNTP_SYNC_MODE_IMMED);

    /* Start SNTP service */
    esp_sntp_init();

    initialized = true;

    ESP_LOGI(TAG, "[TIME] Initialized, waiting for sync...");
    ESP_LOGI(TAG, "[TIME] NTP servers: %s, %s, %s",
             NTP_SERVER_PRIMARY, NTP_SERVER_SECONDARY, NTP_SERVER_TERTIARY);

    return ESP_OK;
}

bool time_is_synchronized(void)
{
    return synchronized;
}

esp_err_t time_get_utc(time_t *out_time)
{
    if (out_time == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    time(out_time);

    /* Check if time is reasonable (after 2020-01-01) */
    if (*out_time < 1577836800) {
        return ESP_ERR_INVALID_STATE;
    }

    return ESP_OK;
}

esp_err_t time_get_string(char *buffer, size_t buf_size)
{
    if (buffer == NULL || buf_size == 0) {
        return ESP_ERR_INVALID_ARG;
    }

    time_t now;
    esp_err_t ret = time_get_utc(&now);
    if (ret != ESP_OK) {
        snprintf(buffer, buf_size, "NOT_SYNCED");
        return ret;
    }

    struct tm timeinfo;
    gmtime_r(&now, &timeinfo);

    strftime(buffer, buf_size, "%Y-%m-%d %H:%M:%S UTC", &timeinfo);

    return ESP_OK;
}

esp_err_t time_wait_for_sync(uint32_t timeout_ms)
{
    if (!initialized) {
        ESP_LOGE(TAG, "[TIME] Not initialized");
        return ESP_ERR_INVALID_STATE;
    }

    if (synchronized) {
        return ESP_OK;
    }

    ESP_LOGI(TAG, "[TIME] Waiting for synchronization (timeout: %lu ms)", timeout_ms);

    TickType_t timeout_ticks = (timeout_ms == 0) ? portMAX_DELAY : pdMS_TO_TICKS(timeout_ms);

    EventBits_t bits = xEventGroupWaitBits(
        time_event_group,
        TIME_SYNCED_BIT,
        pdFALSE,  /* Don't clear on exit */
        pdTRUE,   /* Wait for all bits */
        timeout_ticks
    );

    if (bits & TIME_SYNCED_BIT) {
        return ESP_OK;
    } else {
        ESP_LOGW(TAG, "[TIME] Synchronization timeout");
        return ESP_ERR_TIMEOUT;
    }
}
