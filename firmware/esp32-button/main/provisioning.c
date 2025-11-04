#include "provisioning.h"
#include <string.h>
#include "esp_log.h"
#include "nvs_flash.h"
#include "nvs.h"

static const char *TAG = "PROVISION";

/* NVS handle for provisioning namespace */
static nvs_handle_t nvs_handle;
static bool nvs_initialized = false;

esp_err_t provision_init(void)
{
    esp_err_t ret;
    nvs_sec_cfg_t cfg;

    /* Initialize default (unencrypted) NVS partition for basic system data */
    ret = nvs_flash_init();
    if (ret == ESP_ERR_NVS_NO_FREE_PAGES || ret == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_LOGW(TAG, "Default NVS partition needs erasing, erasing...");
        ESP_ERROR_CHECK(nvs_flash_erase());
        ret = nvs_flash_init();
    }
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to initialize default NVS: %s", esp_err_to_name(ret));
        return ret;
    }

    /* Read encryption keys from nvs_key partition
     * On first boot, generates keys and stores them in nvs_key partition
     * On subsequent boots, reads existing keys from nvs_key partition
     */
    ret = nvs_flash_read_security_cfg_v2(&cfg);
    if (ret == ESP_ERR_NVS_SEC_NOT_FOUND) {
        /* First boot - generate new encryption keys */
        ESP_LOGI(TAG, "First boot detected, generating NVS encryption keys...");
        ret = nvs_flash_generate_keys_v2(&cfg);
        if (ret != ESP_OK) {
            ESP_LOGE(TAG, "Failed to generate NVS encryption keys: %s", esp_err_to_name(ret));
            return ret;
        }
        ESP_LOGI(TAG, "NVS encryption keys generated and stored in nvs_key partition");
    } else if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to read NVS security config: %s", esp_err_to_name(ret));
        return ret;
    } else {
        ESP_LOGI(TAG, "NVS encryption keys loaded from nvs_key partition");
    }

    /* Initialize encrypted NVS partition with secure API
     * This ensures all credential data is encrypted at rest using keys from nvs_key
     */
    ret = nvs_flash_secure_init_partition("nvs", &cfg);
    if (ret == ESP_ERR_NVS_NO_FREE_PAGES || ret == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_LOGW(TAG, "Encrypted NVS partition needs erasing, erasing...");
        ESP_ERROR_CHECK(nvs_flash_erase_partition("nvs"));
        ret = nvs_flash_secure_init_partition("nvs", &cfg);
    }
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to initialize encrypted NVS partition: %s", esp_err_to_name(ret));
        return ret;
    }

    nvs_initialized = true;
    ESP_LOGI(TAG, "Provisioning system initialized (NVS encryption ENABLED via secure API)");

    return ESP_OK;
}

bool provision_is_provisioned(void)
{
    nvs_handle_t handle;
    esp_err_t ret;
    uint8_t provisioned = 0;

    if (!nvs_initialized) {
        ESP_LOGE(TAG, "Provisioning not initialized");
        return false;
    }

    ret = nvs_open(PROVISION_NAMESPACE, NVS_READONLY, &handle);
    if (ret != ESP_OK) {
        return false;
    }

    ret = nvs_get_u8(handle, PROVISION_KEY_PROVISIONED, &provisioned);
    nvs_close(handle);

    return (ret == ESP_OK && provisioned == 1);
}

esp_err_t provision_mark_provisioned(void)
{
    nvs_handle_t handle;
    esp_err_t ret;
    uint8_t provisioned = 1;

    if (!nvs_initialized) {
        ESP_LOGE(TAG, "Provisioning not initialized");
        return ESP_ERR_INVALID_STATE;
    }

    ret = nvs_open(PROVISION_NAMESPACE, NVS_READWRITE, &handle);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to open NVS: %s", esp_err_to_name(ret));
        return ret;
    }

    ret = nvs_set_u8(handle, PROVISION_KEY_PROVISIONED, provisioned);
    if (ret == ESP_OK) {
        ret = nvs_commit(handle);
    }

    nvs_close(handle);

    if (ret == ESP_OK) {
        ESP_LOGI(TAG, "Device marked as provisioned");
    } else {
        ESP_LOGE(TAG, "Failed to mark provisioned: %s", esp_err_to_name(ret));
    }

    return ret;
}

esp_err_t provision_set_string(const char *key, const char *value)
{
    nvs_handle_t handle;
    esp_err_t ret;

    if (key == NULL || value == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    if (!nvs_initialized) {
        ESP_LOGE(TAG, "Provisioning not initialized");
        return ESP_ERR_INVALID_STATE;
    }

    ret = nvs_open(PROVISION_NAMESPACE, NVS_READWRITE, &handle);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to open NVS: %s", esp_err_to_name(ret));
        return ret;
    }

    ret = nvs_set_str(handle, key, value);
    if (ret == ESP_OK) {
        ret = nvs_commit(handle);
    }

    nvs_close(handle);

    if (ret == ESP_OK) {
        ESP_LOGD(TAG, "Saved: %s", key);
    } else {
        ESP_LOGE(TAG, "Failed to save %s: %s", key, esp_err_to_name(ret));
    }

    return ret;
}

esp_err_t provision_get_string(const char *key, char *value, size_t max_len)
{
    nvs_handle_t handle;
    esp_err_t ret;
    size_t required_size = 0;

    if (key == NULL || value == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    if (!nvs_initialized) {
        ESP_LOGE(TAG, "Provisioning not initialized");
        return ESP_ERR_INVALID_STATE;
    }

    ret = nvs_open(PROVISION_NAMESPACE, NVS_READONLY, &handle);
    if (ret != ESP_OK) {
        return ret;
    }

    /* Get required size first */
    ret = nvs_get_str(handle, key, NULL, &required_size);
    if (ret != ESP_OK) {
        nvs_close(handle);
        return ret;
    }

    if (required_size > max_len) {
        nvs_close(handle);
        ESP_LOGE(TAG, "Buffer too small for %s: need %zu, have %zu", key, required_size, max_len);
        return ESP_ERR_INVALID_SIZE;
    }

    /* Read value */
    size_t actual_size = max_len;
    ret = nvs_get_str(handle, key, value, &actual_size);
    nvs_close(handle);

    if (ret == ESP_OK) {
        ESP_LOGD(TAG, "Loaded: %s", key);
    }

    return ret;
}

esp_err_t provision_save_config(const device_config_t *config)
{
    esp_err_t ret;

    if (config == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    ESP_LOGI(TAG, "Saving device configuration...");

    /* Validate fields are not empty */
    if (strlen(config->wifi_ssid) == 0 || strlen(config->device_id) == 0) {
        ESP_LOGE(TAG, "WiFi SSID and Device ID are required");
        return ESP_ERR_INVALID_ARG;
    }

    /* Save each field */
    ret = provision_set_string(PROVISION_KEY_WIFI_SSID, config->wifi_ssid);
    if (ret != ESP_OK) return ret;

    ret = provision_set_string(PROVISION_KEY_WIFI_PASS, config->wifi_password);
    if (ret != ESP_OK) return ret;

    ret = provision_set_string(PROVISION_KEY_DEVICE_ID, config->device_id);
    if (ret != ESP_OK) return ret;

    ret = provision_set_string(PROVISION_KEY_TENANT_ID, config->tenant_id);
    if (ret != ESP_OK) return ret;

    ret = provision_set_string(PROVISION_KEY_BUILDING_ID, config->building_id);
    if (ret != ESP_OK) return ret;

    ret = provision_set_string(PROVISION_KEY_ROOM_ID, config->room_id);
    if (ret != ESP_OK) return ret;

    ESP_LOGI(TAG, "Device configuration saved successfully");
    ESP_LOGI(TAG, "  Device ID: %s", config->device_id);
    ESP_LOGI(TAG, "  Tenant: %s", config->tenant_id);
    ESP_LOGI(TAG, "  Building: %s", config->building_id);
    ESP_LOGI(TAG, "  Room: %s", config->room_id);

    return ESP_OK;
}

esp_err_t provision_load_config(device_config_t *config)
{
    esp_err_t ret;

    if (config == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    memset(config, 0, sizeof(device_config_t));

    ESP_LOGI(TAG, "Loading device configuration...");

    /* Load each field */
    ret = provision_get_string(PROVISION_KEY_WIFI_SSID, config->wifi_ssid, MAX_WIFI_SSID_LEN);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to load WiFi SSID");
        return ret;
    }

    ret = provision_get_string(PROVISION_KEY_WIFI_PASS, config->wifi_password, MAX_WIFI_PASS_LEN);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to load WiFi password");
        return ret;
    }

    ret = provision_get_string(PROVISION_KEY_DEVICE_ID, config->device_id, MAX_DEVICE_ID_LEN);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to load Device ID");
        return ret;
    }

    ret = provision_get_string(PROVISION_KEY_TENANT_ID, config->tenant_id, MAX_TENANT_ID_LEN);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to load Tenant ID");
        return ret;
    }

    ret = provision_get_string(PROVISION_KEY_BUILDING_ID, config->building_id, MAX_BUILDING_ID_LEN);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to load Building ID");
        return ret;
    }

    ret = provision_get_string(PROVISION_KEY_ROOM_ID, config->room_id, MAX_ROOM_ID_LEN);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to load Room ID");
        return ret;
    }

    ESP_LOGI(TAG, "Device configuration loaded successfully");
    ESP_LOGI(TAG, "  Device ID: %s", config->device_id);
    ESP_LOGI(TAG, "  Tenant: %s", config->tenant_id);
    ESP_LOGI(TAG, "  Building: %s", config->building_id);
    ESP_LOGI(TAG, "  Room: %s", config->room_id);

    return ESP_OK;
}

esp_err_t provision_save_certificates(const char *ca_cert,
                                       const char *client_cert,
                                       const char *client_key)
{
    esp_err_t ret;

    if (ca_cert == NULL || client_cert == NULL || client_key == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    /* Validate certificate sizes */
    if (strlen(ca_cert) >= MAX_CERT_LEN ||
        strlen(client_cert) >= MAX_CERT_LEN ||
        strlen(client_key) >= MAX_KEY_LEN) {
        ESP_LOGE(TAG, "Certificate or key exceeds maximum length");
        return ESP_ERR_INVALID_SIZE;
    }

    ESP_LOGI(TAG, "Saving TLS certificates...");

    ret = provision_set_string(PROVISION_KEY_CA_CERT, ca_cert);
    if (ret != ESP_OK) return ret;

    ret = provision_set_string(PROVISION_KEY_CLIENT_CERT, client_cert);
    if (ret != ESP_OK) return ret;

    ret = provision_set_string(PROVISION_KEY_CLIENT_KEY, client_key);
    if (ret != ESP_OK) return ret;

    ESP_LOGI(TAG, "TLS certificates saved successfully");

    return ESP_OK;
}

esp_err_t provision_load_certificates(device_certs_t *certs)
{
    esp_err_t ret;
    size_t required_size;
    nvs_handle_t handle;

    if (certs == NULL) {
        return ESP_ERR_INVALID_ARG;
    }

    memset(certs, 0, sizeof(device_certs_t));

    if (!nvs_initialized) {
        ESP_LOGE(TAG, "Provisioning not initialized");
        return ESP_ERR_INVALID_STATE;
    }

    ret = nvs_open(PROVISION_NAMESPACE, NVS_READONLY, &handle);
    if (ret != ESP_OK) {
        return ret;
    }

    ESP_LOGI(TAG, "Loading TLS certificates...");

    /* Load CA certificate */
    ret = nvs_get_str(handle, PROVISION_KEY_CA_CERT, NULL, &required_size);
    if (ret != ESP_OK) {
        nvs_close(handle);
        return ret;
    }

    certs->ca_cert = malloc(required_size);
    if (certs->ca_cert == NULL) {
        nvs_close(handle);
        return ESP_ERR_NO_MEM;
    }

    ret = nvs_get_str(handle, PROVISION_KEY_CA_CERT, certs->ca_cert, &required_size);
    if (ret != ESP_OK) {
        free(certs->ca_cert);
        nvs_close(handle);
        return ret;
    }

    /* Load client certificate */
    ret = nvs_get_str(handle, PROVISION_KEY_CLIENT_CERT, NULL, &required_size);
    if (ret != ESP_OK) {
        free(certs->ca_cert);
        nvs_close(handle);
        return ret;
    }

    certs->client_cert = malloc(required_size);
    if (certs->client_cert == NULL) {
        free(certs->ca_cert);
        nvs_close(handle);
        return ESP_ERR_NO_MEM;
    }

    ret = nvs_get_str(handle, PROVISION_KEY_CLIENT_CERT, certs->client_cert, &required_size);
    if (ret != ESP_OK) {
        free(certs->ca_cert);
        free(certs->client_cert);
        nvs_close(handle);
        return ret;
    }

    /* Load client private key */
    ret = nvs_get_str(handle, PROVISION_KEY_CLIENT_KEY, NULL, &required_size);
    if (ret != ESP_OK) {
        free(certs->ca_cert);
        free(certs->client_cert);
        nvs_close(handle);
        return ret;
    }

    certs->client_key = malloc(required_size);
    if (certs->client_key == NULL) {
        free(certs->ca_cert);
        free(certs->client_cert);
        nvs_close(handle);
        return ESP_ERR_NO_MEM;
    }

    ret = nvs_get_str(handle, PROVISION_KEY_CLIENT_KEY, certs->client_key, &required_size);
    if (ret != ESP_OK) {
        free(certs->ca_cert);
        free(certs->client_cert);
        free(certs->client_key);
        nvs_close(handle);
        return ret;
    }

    nvs_close(handle);

    ESP_LOGI(TAG, "TLS certificates loaded successfully");

    return ESP_OK;
}

void provision_free_certificates(device_certs_t *certs)
{
    if (certs == NULL) {
        return;
    }

    if (certs->ca_cert != NULL) {
        free(certs->ca_cert);
        certs->ca_cert = NULL;
    }

    if (certs->client_cert != NULL) {
        free(certs->client_cert);
        certs->client_cert = NULL;
    }

    if (certs->client_key != NULL) {
        free(certs->client_key);
        certs->client_key = NULL;
    }
}

esp_err_t provision_clear(void)
{
    nvs_handle_t handle;
    esp_err_t ret;

    if (!nvs_initialized) {
        ESP_LOGE(TAG, "Provisioning not initialized");
        return ESP_ERR_INVALID_STATE;
    }

    ESP_LOGW(TAG, "Clearing all provisioning data (factory reset)...");

    ret = nvs_open(PROVISION_NAMESPACE, NVS_READWRITE, &handle);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Failed to open NVS: %s", esp_err_to_name(ret));
        return ret;
    }

    /* Erase all keys in namespace */
    ret = nvs_erase_all(handle);
    if (ret == ESP_OK) {
        ret = nvs_commit(handle);
    }

    nvs_close(handle);

    if (ret == ESP_OK) {
        ESP_LOGI(TAG, "All provisioning data cleared successfully");
    } else {
        ESP_LOGE(TAG, "Failed to clear provisioning data: %s", esp_err_to_name(ret));
    }

    return ret;
}
