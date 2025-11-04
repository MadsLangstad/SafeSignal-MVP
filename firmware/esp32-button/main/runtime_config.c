/**
 * SafeSignal Runtime Configuration Implementation
 */

#include "runtime_config.h"
#include "provisioning.h"
#include "config.h"
#include <string.h>
#include "esp_log.h"

static const char *TAG = "RUNTIME_CFG";

/* Global runtime configuration */
static runtime_config_t g_runtime_config = {
    .device_id = DEVICE_ID,      /* Fallback to compile-time default */
    .tenant_id = TENANT_ID,
    .building_id = BUILDING_ID,
    .room_id = ROOM_ID,
    .loaded = false
};

esp_err_t runtime_config_load(void)
{
    device_config_t prov_config;

    /* Try to load from NVS provisioning */
    esp_err_t ret = provision_load_config(&prov_config);

    if (ret == ESP_OK) {
        /* Successfully loaded from NVS */
        strncpy(g_runtime_config.device_id, prov_config.device_id,
                sizeof(g_runtime_config.device_id) - 1);
        g_runtime_config.device_id[sizeof(g_runtime_config.device_id) - 1] = '\0';

        strncpy(g_runtime_config.tenant_id, prov_config.tenant_id,
                sizeof(g_runtime_config.tenant_id) - 1);
        g_runtime_config.tenant_id[sizeof(g_runtime_config.tenant_id) - 1] = '\0';

        strncpy(g_runtime_config.building_id, prov_config.building_id,
                sizeof(g_runtime_config.building_id) - 1);
        g_runtime_config.building_id[sizeof(g_runtime_config.building_id) - 1] = '\0';

        strncpy(g_runtime_config.room_id, prov_config.room_id,
                sizeof(g_runtime_config.room_id) - 1);
        g_runtime_config.room_id[sizeof(g_runtime_config.room_id) - 1] = '\0';

        g_runtime_config.loaded = true;

        ESP_LOGI(TAG, "Runtime config loaded from NVS:");
        ESP_LOGI(TAG, "  Device ID:   %s", g_runtime_config.device_id);
        ESP_LOGI(TAG, "  Tenant ID:   %s", g_runtime_config.tenant_id);
        ESP_LOGI(TAG, "  Building ID: %s", g_runtime_config.building_id);
        ESP_LOGI(TAG, "  Room ID:     %s", g_runtime_config.room_id);

        return ESP_OK;
    }
    else if (ret == ESP_ERR_NVS_NOT_FOUND) {
        /* Not provisioned, use compile-time defaults */
        ESP_LOGW(TAG, "Device not provisioned, using compile-time defaults:");
        ESP_LOGW(TAG, "  Device ID:   %s", g_runtime_config.device_id);
        ESP_LOGW(TAG, "  Tenant ID:   %s", g_runtime_config.tenant_id);
        ESP_LOGW(TAG, "  Building ID: %s", g_runtime_config.building_id);
        ESP_LOGW(TAG, "  Room ID:     %s", g_runtime_config.room_id);
        ESP_LOGW(TAG, "Note: Provision device for unique configuration");

        g_runtime_config.loaded = false;
        return ESP_ERR_NOT_FOUND;
    }
    else {
        /* Error loading from NVS */
        ESP_LOGE(TAG, "Failed to load runtime config from NVS: %s", esp_err_to_name(ret));
        ESP_LOGW(TAG, "Using compile-time defaults");

        g_runtime_config.loaded = false;
        return ret;
    }
}

const runtime_config_t* runtime_config_get(void)
{
    return &g_runtime_config;
}

const char* runtime_config_get_device_id(void)
{
    return g_runtime_config.device_id;
}

const char* runtime_config_get_tenant_id(void)
{
    return g_runtime_config.tenant_id;
}

const char* runtime_config_get_building_id(void)
{
    return g_runtime_config.building_id;
}

const char* runtime_config_get_room_id(void)
{
    return g_runtime_config.room_id;
}
