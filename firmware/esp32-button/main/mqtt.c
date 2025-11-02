#include "mqtt.h"
#include "config.h"
#include "wifi.h"
#include "alert_queue.h"

#include <stdio.h>
#include <string.h>
#include <time.h>
#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"

#include "esp_log.h"
#include "mqtt_client.h"

static const char *TAG = "MQTT";

/* Event group */
extern EventGroupHandle_t system_events;
extern const int MQTT_CONNECTED_BIT;

/* MQTT client handle */
static esp_mqtt_client_handle_t client = NULL;
static bool connected = false;

/* Embedded certificates (defined in component.mk) */
extern const uint8_t ca_cert_start[] asm("_binary_ca_crt_start");
extern const uint8_t ca_cert_end[] asm("_binary_ca_crt_end");
extern const uint8_t client_cert_start[] asm("_binary_client_crt_start");
extern const uint8_t client_cert_end[] asm("_binary_client_crt_end");
extern const uint8_t client_key_start[] asm("_binary_client_key_start");
extern const uint8_t client_key_end[] asm("_binary_client_key_end");

/* Event handler */
static void mqtt_event_handler(void *handler_args, esp_event_base_t base,
                                int32_t event_id, void *event_data)
{
    esp_mqtt_event_handle_t event = event_data;

    switch ((esp_mqtt_event_id_t)event_id) {
        case MQTT_EVENT_CONNECTED:
            ESP_LOGI(TAG, "[MQTT] Connected to broker");
            connected = true;
            xEventGroupSetBits(system_events, MQTT_CONNECTED_BIT);

            /* Process any pending alerts from queue */
            int delivered = alert_queue_process();
            if (delivered > 0) {
                ESP_LOGI(TAG, "[MQTT] Delivered %d queued alerts on reconnect", delivered);
            }
            break;

        case MQTT_EVENT_DISCONNECTED:
            ESP_LOGW(TAG, "[MQTT] Disconnected from broker");
            connected = false;
            xEventGroupClearBits(system_events, MQTT_CONNECTED_BIT);
            break;

        case MQTT_EVENT_SUBSCRIBED:
            ESP_LOGI(TAG, "[MQTT] Subscribed, msg_id=%d", event->msg_id);
            break;

        case MQTT_EVENT_PUBLISHED:
            ESP_LOGD(TAG, "[MQTT] Published, msg_id=%d", event->msg_id);
            break;

        case MQTT_EVENT_DATA:
            ESP_LOGI(TAG, "[MQTT] Data received on topic: %.*s",
                     event->topic_len, event->topic);
            /* Handle incoming messages (future: OTA commands, config updates) */
            break;

        case MQTT_EVENT_ERROR:
            ESP_LOGE(TAG, "[MQTT] Error: type=%d", event->error_handle->error_type);
            break;

        default:
            break;
    }
}

void mqtt_init(void)
{
    ESP_LOGI(TAG, "[MQTT] Initializing...");

    /* Wait for WiFi connection */
    xEventGroupWaitBits(system_events, WIFI_CONNECTED_BIT, pdFALSE, pdTRUE, portMAX_DELAY);

    /* MQTT configuration with mTLS */
    esp_mqtt_client_config_t mqtt_cfg = {
        .broker.address.uri = MQTT_BROKER_URI,
        .broker.verification.certificate = (const char *)ca_cert_start,
        .credentials = {
            .authentication = {
                .certificate = (const char *)client_cert_start,
                .key = (const char *)client_key_start,
            },
        },
        .session.keepalive = MQTT_KEEPALIVE_SECONDS,
        .network.reconnect_timeout_ms = MQTT_RECONNECT_INTERVAL_MS,
    };

    client = esp_mqtt_client_init(&mqtt_cfg);
    if (client == NULL) {
        ESP_LOGE(TAG, "[MQTT] Failed to initialize client");
        return;
    }

    /* Register event handler */
    esp_mqtt_client_register_event(client, ESP_EVENT_ANY_ID, mqtt_event_handler, NULL);

    /* Start MQTT client */
    esp_mqtt_client_start(client);

    ESP_LOGI(TAG, "[MQTT] Client started");
}

bool mqtt_publish_alert(void)
{
    /* Get UTC timestamp (will be 0 if not synchronized yet) */
    time_t now;
    time(&now);
    uint32_t timestamp_utc = (uint32_t)now;
    uint32_t alert_id = xTaskGetTickCount();  /* Use tick count for unique ID */

    /* Create queued alert structure */
    queued_alert_t queued_alert = {
        .alert_id = alert_id,
        .timestamp = timestamp_utc,
        .retry_count = 0,
        .created_at = xTaskGetTickCount() * portTICK_PERIOD_MS / 1000,
        .mode = DEFAULT_ALERT_MODE,
    };

    strncpy(queued_alert.device_id, DEVICE_ID, sizeof(queued_alert.device_id) - 1);
    strncpy(queued_alert.tenant_id, TENANT_ID, sizeof(queued_alert.tenant_id) - 1);
    strncpy(queued_alert.building_id, BUILDING_ID, sizeof(queued_alert.building_id) - 1);
    strncpy(queued_alert.room_id, ROOM_ID, sizeof(queued_alert.room_id) - 1);
    strncpy(queued_alert.version, SAFESIGNAL_VERSION, sizeof(queued_alert.version) - 1);

    /* Enqueue alert for persistence */
    esp_err_t ret = alert_queue_enqueue(&queued_alert);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "[MQTT] Failed to enqueue alert: %s", esp_err_to_name(ret));
        return false;
    }

    /* Attempt immediate publish if connected */
    if (connected && client != NULL) {
        if (mqtt_publish_alert_from_queue(&queued_alert)) {
            ESP_LOGI(TAG, "[MQTT] Alert published immediately");
            /* Note: alert_queue will remove from NVS on next process() call */
            alert_queue_process();  /* Clean up successful alert */
            return true;
        } else {
            ESP_LOGW(TAG, "[MQTT] Immediate publish failed, alert queued for retry");
            return false;
        }
    } else {
        ESP_LOGW(TAG, "[MQTT] Not connected, alert queued for delivery");
        return false;
    }
}

bool mqtt_publish_alert_from_queue(const queued_alert_t *alert)
{
    if (!connected || client == NULL || alert == NULL) {
        return false;
    }

    /* Build JSON payload */
    char payload[PAYLOAD_BUFFER_SIZE];
    int len = snprintf(payload, sizeof(payload),
        "{"
        "\"alertId\":\"ESP32-%s-%lu\","
        "\"deviceId\":\"%s\","
        "\"tenantId\":\"%s\","
        "\"buildingId\":\"%s\","
        "\"sourceRoomId\":\"%s\","
        "\"mode\":%d,"
        "\"origin\":\"ESP32\","
        "\"timestamp\":%lu,"
        "\"retryCount\":%lu,"
        "\"version\":\"%s\""
        "}",
        alert->device_id, alert->alert_id,
        alert->device_id,
        alert->tenant_id,
        alert->building_id,
        alert->room_id,
        alert->mode,
        (unsigned long)alert->timestamp,
        alert->retry_count,
        alert->version
    );

    if (len < 0 || len >= sizeof(payload)) {
        ESP_LOGE(TAG, "[MQTT] Payload buffer overflow");
        return false;
    }

    /* Build topic: safesignal/{tenant}/{building}/alerts/trigger */
    char topic[TOPIC_BUFFER_SIZE];
    snprintf(topic, sizeof(topic), "safesignal/%s/%s/alerts/trigger",
             alert->tenant_id, alert->building_id);

    /* Publish with QoS 1 */
    int msg_id = esp_mqtt_client_publish(client, topic, payload, len, MQTT_QOS, 0);

    if (msg_id >= 0) {
        ESP_LOGI(TAG, "[MQTT] Alert %lu published (msg_id=%d)", alert->alert_id, msg_id);
        return true;
    } else {
        ESP_LOGE(TAG, "[MQTT] Failed to publish alert %lu", alert->alert_id);
        return false;
    }
}

bool mqtt_publish_status(void)
{
    if (!connected || client == NULL) {
        return false;
    }

    uint32_t uptime = xTaskGetTickCount() * portTICK_PERIOD_MS / 1000;
    int8_t rssi = wifi_get_rssi();
    uint32_t free_heap = esp_get_free_heap_size();

    char payload[PAYLOAD_BUFFER_SIZE];
    int len = snprintf(payload, sizeof(payload),
        "{"
        "\"deviceId\":\"%s\","
        "\"tenantId\":\"%s\","
        "\"buildingId\":\"%s\","
        "\"roomId\":\"%s\","
        "\"type\":\"STATUS\","
        "\"timestamp\":%lu,"
        "\"rssi\":%d,"
        "\"uptime\":%lu,"
        "\"freeHeap\":%lu,"
        "\"version\":\"%s\""
        "}",
        DEVICE_ID,
        TENANT_ID,
        BUILDING_ID,
        ROOM_ID,
        xTaskGetTickCount() * portTICK_PERIOD_MS,
        rssi,
        uptime,
        free_heap,
        SAFESIGNAL_VERSION
    );

    if (len < 0 || len >= sizeof(payload)) {
        return false;
    }

    char topic[TOPIC_BUFFER_SIZE];
    snprintf(topic, sizeof(topic), "safesignal/%s/%s/device/status",
             TENANT_ID, BUILDING_ID);

    int msg_id = esp_mqtt_client_publish(client, topic, payload, len, 0, 0);

    if (msg_id >= 0) {
        ESP_LOGI(TAG, "[STATUS] Published (RSSI: %d dBm, Uptime: %lu s)", rssi, uptime);
        return true;
    }

    return false;
}

bool mqtt_publish_heartbeat(void)
{
    if (!connected || client == NULL) {
        return false;
    }

    char payload[128];
    int len = snprintf(payload, sizeof(payload),
        "{\"deviceId\":\"%s\",\"type\":\"HEARTBEAT\",\"timestamp\":%lu}",
        DEVICE_ID,
        xTaskGetTickCount() * portTICK_PERIOD_MS
    );

    char topic[TOPIC_BUFFER_SIZE];
    snprintf(topic, sizeof(topic), "safesignal/%s/%s/device/heartbeat",
             TENANT_ID, BUILDING_ID);

    int msg_id = esp_mqtt_client_publish(client, topic, payload, len, 0, 0);

    return (msg_id >= 0);
}

bool mqtt_is_connected(void)
{
    return connected;
}
