#include "alert_queue.h"
#include "mqtt.h"
#include "config.h"

#include <string.h>
#include "esp_log.h"
#include "nvs_flash.h"
#include "nvs.h"

static const char *TAG = "ALERT_QUEUE";

#define NVS_NAMESPACE "alert_queue"
#define NVS_KEY_COUNT "count"
#define NVS_KEY_STATS "stats"
#define NVS_KEY_ALERT_PREFIX "alert_"

/* Queue state */
static nvs_handle_t nvs_handle;
static bool initialized = false;
static alert_queue_stats_t stats = {0};

/* Helper: Generate NVS key for alert index */
static void get_alert_key(uint32_t index, char *key_buf, size_t buf_size)
{
    snprintf(key_buf, buf_size, "%s%lu", NVS_KEY_ALERT_PREFIX, index);
}

/* Helper: Load stats from NVS */
static esp_err_t load_stats(void)
{
    size_t required_size = sizeof(alert_queue_stats_t);
    esp_err_t ret = nvs_get_blob(nvs_handle, NVS_KEY_STATS, &stats, &required_size);

    if (ret == ESP_ERR_NVS_NOT_FOUND) {
        /* First run, initialize stats */
        memset(&stats, 0, sizeof(stats));
        return ESP_OK;
    }

    return ret;
}

/* Helper: Save stats to NVS */
static esp_err_t save_stats(void)
{
    esp_err_t ret = nvs_set_blob(nvs_handle, NVS_KEY_STATS, &stats, sizeof(stats));
    if (ret == ESP_OK) {
        ret = nvs_commit(nvs_handle);
    }
    return ret;
}

esp_err_t alert_queue_init(void)
{
    if (initialized) {
        return ESP_OK;
    }

    /* Open NVS namespace */
    esp_err_t ret = nvs_open(NVS_NAMESPACE, NVS_READWRITE, &nvs_handle);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "[QUEUE] Failed to open NVS: %s", esp_err_to_name(ret));
        return ret;
    }

    /* Load statistics */
    ret = load_stats();
    if (ret != ESP_OK) {
        ESP_LOGW(TAG, "[QUEUE] Failed to load stats: %s", esp_err_to_name(ret));
        /* Continue anyway with zeroed stats */
    }

    /* Load pending count */
    uint32_t count = 0;
    ret = nvs_get_u32(nvs_handle, NVS_KEY_COUNT, &count);
    if (ret == ESP_ERR_NVS_NOT_FOUND) {
        count = 0;
    } else if (ret != ESP_OK) {
        ESP_LOGW(TAG, "[QUEUE] Failed to load count: %s", esp_err_to_name(ret));
        count = 0;
    }

    stats.pending_count = count;
    initialized = true;

    ESP_LOGI(TAG, "[QUEUE] Initialized: %lu pending alerts", count);

    return ESP_OK;
}

esp_err_t alert_queue_enqueue(const queued_alert_t *alert)
{
    if (!initialized) {
        ESP_LOGE(TAG, "[QUEUE] Not initialized");
        return ESP_ERR_INVALID_STATE;
    }

    if (stats.pending_count >= ALERT_QUEUE_MAX_SIZE) {
        ESP_LOGE(TAG, "[QUEUE] Queue full (%d alerts)", ALERT_QUEUE_MAX_SIZE);
        return ESP_ERR_NO_MEM;
    }

    /* Find next available slot */
    uint32_t index = 0;
    char key[32];

    for (uint32_t i = 0; i < ALERT_QUEUE_MAX_SIZE; i++) {
        get_alert_key(i, key, sizeof(key));

        size_t required_size = 0;
        esp_err_t ret = nvs_get_blob(nvs_handle, key, NULL, &required_size);

        if (ret == ESP_ERR_NVS_NOT_FOUND) {
            /* Found empty slot */
            index = i;
            break;
        }
    }

    /* Store alert in NVS */
    get_alert_key(index, key, sizeof(key));
    esp_err_t ret = nvs_set_blob(nvs_handle, key, alert, sizeof(queued_alert_t));

    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "[QUEUE] Failed to store alert: %s", esp_err_to_name(ret));
        return ret;
    }

    /* Update count */
    stats.pending_count++;
    stats.total_enqueued++;

    ret = nvs_set_u32(nvs_handle, NVS_KEY_COUNT, stats.pending_count);
    if (ret != ESP_OK) {
        ESP_LOGW(TAG, "[QUEUE] Failed to update count: %s", esp_err_to_name(ret));
    }

    /* Commit changes */
    ret = nvs_commit(nvs_handle);
    if (ret != ESP_OK) {
        ESP_LOGW(TAG, "[QUEUE] Failed to commit: %s", esp_err_to_name(ret));
    }

    /* Save stats */
    save_stats();

    ESP_LOGI(TAG, "[QUEUE] Enqueued alert %lu (index %lu, %lu pending)",
             alert->alert_id, index, stats.pending_count);

    return ESP_OK;
}

int alert_queue_process(void)
{
    if (!initialized) {
        ESP_LOGE(TAG, "[QUEUE] Not initialized");
        return 0;
    }

    if (stats.pending_count == 0) {
        return 0;
    }

    if (!mqtt_is_connected()) {
        ESP_LOGD(TAG, "[QUEUE] MQTT not connected, skipping processing");
        return 0;
    }

    int delivered = 0;
    char key[32];
    queued_alert_t alert;
    size_t required_size;

    ESP_LOGI(TAG, "[QUEUE] Processing %lu pending alerts", stats.pending_count);

    /* Iterate through all possible slots */
    for (uint32_t i = 0; i < ALERT_QUEUE_MAX_SIZE; i++) {
        get_alert_key(i, key, sizeof(key));
        required_size = sizeof(queued_alert_t);

        esp_err_t ret = nvs_get_blob(nvs_handle, key, &alert, &required_size);

        if (ret == ESP_ERR_NVS_NOT_FOUND) {
            continue;  /* Empty slot */
        }

        if (ret != ESP_OK) {
            ESP_LOGW(TAG, "[QUEUE] Failed to read alert %lu: %s", i, esp_err_to_name(ret));
            continue;
        }

        /* Check if alert expired */
        uint32_t now = xTaskGetTickCount() * portTICK_PERIOD_MS / 1000;
        if ((now - alert.created_at) > ALERT_QUEUE_EXPIRY_SECONDS) {
            ESP_LOGW(TAG, "[QUEUE] Alert %lu expired, removing", alert.alert_id);
            nvs_erase_key(nvs_handle, key);
            stats.pending_count--;
            stats.total_expired++;
            save_stats();
            continue;
        }

        /* Check retry limit */
        if (alert.retry_count >= ALERT_QUEUE_MAX_RETRIES) {
            ESP_LOGE(TAG, "[QUEUE] Alert %lu exceeded retry limit, removing", alert.alert_id);
            nvs_erase_key(nvs_handle, key);
            stats.pending_count--;
            stats.total_failed++;
            save_stats();
            continue;
        }

        /* Attempt to publish */
        ESP_LOGI(TAG, "[QUEUE] Attempting delivery of alert %lu (retry %lu)",
                 alert.alert_id, alert.retry_count);

        if (mqtt_publish_alert_from_queue(&alert)) {
            /* Success - remove from queue */
            ESP_LOGI(TAG, "[QUEUE] ✓ Alert %lu delivered", alert.alert_id);
            nvs_erase_key(nvs_handle, key);
            stats.pending_count--;
            stats.total_delivered++;
            delivered++;
            save_stats();
        } else {
            /* Failed - increment retry count */
            ESP_LOGW(TAG, "[QUEUE] ✗ Alert %lu delivery failed", alert.alert_id);
            alert.retry_count++;
            nvs_set_blob(nvs_handle, key, &alert, sizeof(queued_alert_t));
        }
    }

    /* Update count in NVS */
    nvs_set_u32(nvs_handle, NVS_KEY_COUNT, stats.pending_count);
    nvs_commit(nvs_handle);

    ESP_LOGI(TAG, "[QUEUE] Processing complete: %d delivered, %lu remaining",
             delivered, stats.pending_count);

    return delivered;
}

int alert_queue_get_count(void)
{
    return stats.pending_count;
}

int alert_queue_cleanup_expired(void)
{
    if (!initialized) {
        return 0;
    }

    int removed = 0;
    char key[32];
    queued_alert_t alert;
    size_t required_size;
    uint32_t now = xTaskGetTickCount() * portTICK_PERIOD_MS / 1000;

    for (uint32_t i = 0; i < ALERT_QUEUE_MAX_SIZE; i++) {
        get_alert_key(i, key, sizeof(key));
        required_size = sizeof(queued_alert_t);

        esp_err_t ret = nvs_get_blob(nvs_handle, key, &alert, &required_size);

        if (ret == ESP_ERR_NVS_NOT_FOUND) {
            continue;
        }

        if (ret != ESP_OK) {
            continue;
        }

        if ((now - alert.created_at) > ALERT_QUEUE_EXPIRY_SECONDS) {
            nvs_erase_key(nvs_handle, key);
            stats.pending_count--;
            stats.total_expired++;
            removed++;
        }
    }

    if (removed > 0) {
        nvs_set_u32(nvs_handle, NVS_KEY_COUNT, stats.pending_count);
        nvs_commit(nvs_handle);
        save_stats();
        ESP_LOGI(TAG, "[QUEUE] Cleanup: %d expired alerts removed", removed);
    }

    return removed;
}

esp_err_t alert_queue_get_stats(alert_queue_stats_t *out_stats)
{
    if (!initialized || out_stats == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    memcpy(out_stats, &stats, sizeof(alert_queue_stats_t));
    return ESP_OK;
}
