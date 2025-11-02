#include "watchdog.h"
#include "config.h"

#include "esp_log.h"
#include "esp_task_wdt.h"

static const char *TAG = "WATCHDOG";

static bool initialized = false;

esp_err_t watchdog_init(void)
{
    if (initialized) {
        ESP_LOGW(TAG, "[WDT] Already initialized");
        return ESP_OK;
    }

    /* Configure Task Watchdog Timer */
    esp_task_wdt_config_t twdt_config = {
        .timeout_ms = WATCHDOG_TIMEOUT_SECONDS * 1000,
        .idle_core_mask = 0,        /* Don't monitor idle tasks */
        .trigger_panic = true       /* Panic and reboot on timeout */
    };

    esp_err_t ret = esp_task_wdt_init(&twdt_config);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "[WDT] Failed to initialize: %s", esp_err_to_name(ret));
        return ret;
    }

    initialized = true;
    ESP_LOGI(TAG, "[WDT] Initialized (timeout: %d seconds)", WATCHDOG_TIMEOUT_SECONDS);

    return ESP_OK;
}

esp_err_t watchdog_add_task(TaskHandle_t task_handle, const char *task_name)
{
    if (!initialized) {
        ESP_LOGE(TAG, "[WDT] Not initialized");
        return ESP_ERR_INVALID_STATE;
    }

    esp_err_t ret = esp_task_wdt_add(task_handle);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "[WDT] Failed to add task '%s': %s",
                 task_name, esp_err_to_name(ret));
        return ret;
    }

    ESP_LOGI(TAG, "[WDT] Monitoring task: %s", task_name);
    return ESP_OK;
}

esp_err_t watchdog_feed(void)
{
    if (!initialized) {
        return ESP_ERR_INVALID_STATE;
    }

    return esp_task_wdt_reset();
}

esp_err_t watchdog_remove_task(TaskHandle_t task_handle)
{
    if (!initialized) {
        return ESP_ERR_INVALID_STATE;
    }

    return esp_task_wdt_delete(task_handle);
}
