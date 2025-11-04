#include "wifi.h"
#include "config.h"
#include "provisioning.h"

#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/event_groups.h"

#include "esp_wifi.h"
#include "esp_event.h"
#include "esp_log.h"

static const char *TAG = "WIFI";

/* WiFi credentials loaded from NVS */
static char wifi_ssid[MAX_WIFI_SSID_LEN] = {0};
static char wifi_password[MAX_WIFI_PASS_LEN] = {0};

/* Event group for WiFi events */
extern EventGroupHandle_t system_events;
extern const int WIFI_CONNECTED_BIT;

/* WiFi state */
static bool connected = false;
static int8_t rssi = 0;

/* Event handler */
static void wifi_event_handler(void *arg, esp_event_base_t event_base,
                                int32_t event_id, void *event_data)
{
    if (event_base == WIFI_EVENT) {
        switch (event_id) {
            case WIFI_EVENT_STA_START:
                ESP_LOGI(TAG, "[WIFI] Station started, connecting...");
                esp_wifi_connect();
                break;

            case WIFI_EVENT_STA_DISCONNECTED:
                ESP_LOGW(TAG, "[WIFI] Disconnected, reconnecting...");
                connected = false;
                xEventGroupClearBits(system_events, WIFI_CONNECTED_BIT);
                esp_wifi_connect();
                break;

            case WIFI_EVENT_STA_CONNECTED:
                ESP_LOGI(TAG, "[WIFI] Connected to AP");
                break;

            default:
                break;
        }
    } else if (event_base == IP_EVENT) {
        switch (event_id) {
            case IP_EVENT_STA_GOT_IP: {
                ip_event_got_ip_t *event = (ip_event_got_ip_t *)event_data;
                ESP_LOGI(TAG, "[WIFI] Got IP address: " IPSTR, IP2STR(&event->ip_info.ip));
                connected = true;
                xEventGroupSetBits(system_events, WIFI_CONNECTED_BIT);

                /* Get RSSI */
                wifi_ap_record_t ap_info;
                if (esp_wifi_sta_get_ap_info(&ap_info) == ESP_OK) {
                    rssi = ap_info.rssi;
                    ESP_LOGI(TAG, "[WIFI] Signal strength: %d dBm", rssi);
                }
                break;
            }

            default:
                break;
        }
    }
}

esp_err_t wifi_load_credentials(void)
{
    esp_err_t ret;

    ESP_LOGI(TAG, "[WIFI] Loading credentials from NVS...");

    /* Load WiFi credentials from NVS */
    ret = provision_get_string(PROVISION_KEY_WIFI_SSID, wifi_ssid, MAX_WIFI_SSID_LEN);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "[WIFI] Failed to load SSID from NVS: %s", esp_err_to_name(ret));
        return ret;
    }

    ret = provision_get_string(PROVISION_KEY_WIFI_PASS, wifi_password, MAX_WIFI_PASS_LEN);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "[WIFI] Failed to load password from NVS: %s", esp_err_to_name(ret));
        return ret;
    }

    ESP_LOGI(TAG, "[WIFI] Credentials loaded: SSID='%s'", wifi_ssid);

    return ESP_OK;
}

void wifi_init(void)
{
    ESP_LOGI(TAG, "[WIFI] Initializing...");

    /* Load WiFi credentials from NVS */
    esp_err_t ret = wifi_load_credentials();
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "[WIFI] Device not provisioned! Cannot connect to WiFi.");
        ESP_LOGE(TAG, "[WIFI] Please provision device with credentials.");
        return;
    }

    /* Initialize TCP/IP stack */
    ESP_ERROR_CHECK(esp_netif_init());

    /* Create default event loop if not already created */
    esp_netif_create_default_wifi_sta();

    /* Initialize WiFi with default config */
    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&cfg));

    /* Register event handlers */
    ESP_ERROR_CHECK(esp_event_handler_instance_register(
        WIFI_EVENT,
        ESP_EVENT_ANY_ID,
        &wifi_event_handler,
        NULL,
        NULL
    ));

    ESP_ERROR_CHECK(esp_event_handler_instance_register(
        IP_EVENT,
        IP_EVENT_STA_GOT_IP,
        &wifi_event_handler,
        NULL,
        NULL
    ));

    /* Configure WiFi with credentials from NVS */
    wifi_config_t wifi_config = {
        .sta = {
            .threshold.authmode = WIFI_AUTH_WPA2_PSK,
            .pmf_cfg = {
                .capable = true,
                .required = false
            },
        },
    };

    /* Copy credentials into WiFi config */
    strncpy((char *)wifi_config.sta.ssid, wifi_ssid, sizeof(wifi_config.sta.ssid));
    strncpy((char *)wifi_config.sta.password, wifi_password, sizeof(wifi_config.sta.password));

    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &wifi_config));
    ESP_ERROR_CHECK(esp_wifi_start());

    ESP_LOGI(TAG, "[WIFI] Connecting to '%s'...", wifi_ssid);
}

bool wifi_is_connected(void)
{
    return connected;
}

int8_t wifi_get_rssi(void)
{
    if (!connected) {
        return -127;
    }

    wifi_ap_record_t ap_info;
    if (esp_wifi_sta_get_ap_info(&ap_info) == ESP_OK) {
        rssi = ap_info.rssi;
    }

    return rssi;
}
