/**
 * SafeSignal Runtime Configuration
 *
 * Loads device configuration from NVS provisioning and provides
 * global access to runtime config values.
 *
 * Replaces compile-time constants with provisioned values.
 */

#ifndef SAFESIGNAL_RUNTIME_CONFIG_H
#define SAFESIGNAL_RUNTIME_CONFIG_H

#include <stdint.h>
#include <stdbool.h>
#include "esp_err.h"

/* Maximum field sizes */
#define RUNTIME_CONFIG_DEVICE_ID_LEN 32
#define RUNTIME_CONFIG_TENANT_ID_LEN 16
#define RUNTIME_CONFIG_BUILDING_ID_LEN 16
#define RUNTIME_CONFIG_ROOM_ID_LEN 16

/**
 * Runtime configuration structure
 * Loaded from NVS on boot, used throughout application
 */
typedef struct {
    char device_id[RUNTIME_CONFIG_DEVICE_ID_LEN];
    char tenant_id[RUNTIME_CONFIG_TENANT_ID_LEN];
    char building_id[RUNTIME_CONFIG_BUILDING_ID_LEN];
    char room_id[RUNTIME_CONFIG_ROOM_ID_LEN];
    bool loaded;  /* True if config was loaded from NVS */
} runtime_config_t;

/**
 * @brief Load runtime configuration from NVS provisioning
 *
 * Must be called after provision_init() and before any components
 * that need device metadata (MQTT, alerts, etc.)
 *
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_NOT_FOUND if not provisioned (falls back to defaults)
 *  - ESP_ERR_* on other failures
 */
esp_err_t runtime_config_load(void);

/**
 * @brief Get pointer to global runtime configuration
 *
 * @return Pointer to runtime config (never NULL)
 */
const runtime_config_t* runtime_config_get(void);

/**
 * @brief Get device ID
 *
 * @return Device ID string (never NULL)
 */
const char* runtime_config_get_device_id(void);

/**
 * @brief Get tenant ID
 *
 * @return Tenant ID string (never NULL)
 */
const char* runtime_config_get_tenant_id(void);

/**
 * @brief Get building ID
 *
 * @return Building ID string (never NULL)
 */
const char* runtime_config_get_building_id(void);

/**
 * @brief Get room ID
 *
 * @return Room ID string (never NULL)
 */
const char* runtime_config_get_room_id(void);

#endif /* SAFESIGNAL_RUNTIME_CONFIG_H */
