#ifndef SAFESIGNAL_PROVISIONING_H
#define SAFESIGNAL_PROVISIONING_H

#include <stdint.h>
#include <stdbool.h>
#include "esp_err.h"

/* ========================================================================== */
/* SafeSignal ESP32 Secure Provisioning API                                   */
/* ========================================================================== */
/*
 * Stores device configuration and credentials in encrypted NVS.
 * Devices ship blank and are provisioned via BLE on first boot.
 *
 * Security Features:
 * - NVS encryption enabled (keys derived from eFuse)
 * - Credentials never hardcoded in firmware
 * - Factory reset capability for reprovisioning
 */

#define PROVISION_NAMESPACE "safesignal"
#define PROVISION_KEY_WIFI_SSID "wifi_ssid"
#define PROVISION_KEY_WIFI_PASS "wifi_pass"
#define PROVISION_KEY_DEVICE_ID "device_id"
#define PROVISION_KEY_TENANT_ID "tenant_id"
#define PROVISION_KEY_BUILDING_ID "building_id"
#define PROVISION_KEY_ROOM_ID "room_id"
#define PROVISION_KEY_CA_CERT "ca_cert"
#define PROVISION_KEY_CLIENT_CERT "client_cert"
#define PROVISION_KEY_CLIENT_KEY "client_key"
#define PROVISION_KEY_PROVISIONED "provisioned"

/* Maximum sizes for configuration fields */
#define MAX_WIFI_SSID_LEN 32
#define MAX_WIFI_PASS_LEN 64
#define MAX_DEVICE_ID_LEN 32
#define MAX_TENANT_ID_LEN 16
#define MAX_BUILDING_ID_LEN 16
#define MAX_ROOM_ID_LEN 16
#define MAX_CERT_LEN 2048
#define MAX_KEY_LEN 2048

/**
 * @brief Device configuration structure
 */
typedef struct {
    char wifi_ssid[MAX_WIFI_SSID_LEN];
    char wifi_password[MAX_WIFI_PASS_LEN];
    char device_id[MAX_DEVICE_ID_LEN];
    char tenant_id[MAX_TENANT_ID_LEN];
    char building_id[MAX_BUILDING_ID_LEN];
    char room_id[MAX_ROOM_ID_LEN];
} device_config_t;

/**
 * @brief TLS certificate bundle structure
 */
typedef struct {
    char *ca_cert;          /* CA certificate (PEM format) */
    char *client_cert;      /* Client certificate (PEM format) */
    char *client_key;       /* Client private key (PEM format) */
} device_certs_t;

/**
 * @brief Initialize the provisioning system
 *
 * Must be called before any other provisioning functions.
 * Initializes NVS with encryption enabled.
 *
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_NVS_NOT_INITIALIZED if NVS initialization failed
 *  - ESP_ERR_NO_MEM if insufficient memory
 */
esp_err_t provision_init(void);

/**
 * @brief Check if device has been provisioned
 *
 * @return true if provisioned, false otherwise
 */
bool provision_is_provisioned(void);

/**
 * @brief Save device configuration to encrypted NVS
 *
 * @param config Pointer to device configuration structure
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_INVALID_ARG if config is NULL or invalid
 *  - ESP_ERR_NVS_* on NVS operation failure
 */
esp_err_t provision_save_config(const device_config_t *config);

/**
 * @brief Load device configuration from encrypted NVS
 *
 * @param config Pointer to device configuration structure (output)
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_INVALID_ARG if config is NULL
 *  - ESP_ERR_NVS_NOT_FOUND if not provisioned
 *  - ESP_ERR_NVS_* on NVS operation failure
 */
esp_err_t provision_load_config(device_config_t *config);

/**
 * @brief Save TLS certificates to encrypted NVS
 *
 * Stores CA certificate, client certificate, and client private key.
 * Certificates must be in PEM format.
 *
 * @param ca_cert CA certificate (PEM, null-terminated)
 * @param client_cert Client certificate (PEM, null-terminated)
 * @param client_key Client private key (PEM, null-terminated)
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_INVALID_ARG if any certificate is NULL or invalid
 *  - ESP_ERR_INVALID_SIZE if certificate exceeds maximum length
 *  - ESP_ERR_NVS_* on NVS operation failure
 */
esp_err_t provision_save_certificates(const char *ca_cert,
                                       const char *client_cert,
                                       const char *client_key);

/**
 * @brief Load TLS certificates from encrypted NVS
 *
 * Allocates memory for certificates. Caller must free using
 * provision_free_certificates().
 *
 * @param certs Pointer to certificate bundle structure (output)
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_INVALID_ARG if certs is NULL
 *  - ESP_ERR_NVS_NOT_FOUND if certificates not provisioned
 *  - ESP_ERR_NO_MEM if insufficient memory
 *  - ESP_ERR_NVS_* on NVS operation failure
 */
esp_err_t provision_load_certificates(device_certs_t *certs);

/**
 * @brief Free certificate bundle memory
 *
 * Frees memory allocated by provision_load_certificates().
 *
 * @param certs Pointer to certificate bundle structure
 */
void provision_free_certificates(device_certs_t *certs);

/**
 * @brief Mark device as provisioned
 *
 * Sets provisioned flag in NVS. Called after successful provisioning.
 *
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_NVS_* on NVS operation failure
 */
esp_err_t provision_mark_provisioned(void);

/**
 * @brief Clear all provisioning data (factory reset)
 *
 * Erases all configuration and certificates from NVS.
 * Device will require reprovisioning on next boot.
 *
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_NVS_* on NVS operation failure
 */
esp_err_t provision_clear(void);

/**
 * @brief Get individual configuration value
 *
 * Helper function to retrieve a single configuration parameter.
 *
 * @param key Configuration key (use PROVISION_KEY_* defines)
 * @param value Buffer to store value (output)
 * @param max_len Maximum length of value buffer
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_INVALID_ARG if key or value is NULL
 *  - ESP_ERR_NVS_NOT_FOUND if key not found
 *  - ESP_ERR_NVS_* on NVS operation failure
 */
esp_err_t provision_get_string(const char *key, char *value, size_t max_len);

/**
 * @brief Set individual configuration value
 *
 * Helper function to set a single configuration parameter.
 *
 * @param key Configuration key (use PROVISION_KEY_* defines)
 * @param value Value to store (null-terminated string)
 * @return
 *  - ESP_OK on success
 *  - ESP_ERR_INVALID_ARG if key or value is NULL
 *  - ESP_ERR_NVS_* on NVS operation failure
 */
esp_err_t provision_set_string(const char *key, const char *value);

#endif /* SAFESIGNAL_PROVISIONING_H */
