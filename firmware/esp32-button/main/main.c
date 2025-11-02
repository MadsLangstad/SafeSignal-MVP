/**
 * SafeSignal ESP32-S3 Button Firmware
 *
 * Physical panic button for emergency alerting system
 *
 * Features:
 * - WiFi connectivity with auto-reconnect
 * - MQTT client with mTLS authentication
 * - Button press detection with debouncing
 * - Alert publishing to edge gateway
 * - Status reporting (RSSI, uptime, metrics)
 *
 * Hardware: ESP32-S3-DevKitC-1
 * Security: mTLS with client certificates
 * Protocol: MQTT v5 (QoS 1 for alerts)
 */

#include <stdio.h>
#include <stdint.h>
#include <stddef.h>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/semphr.h"
#include "freertos/queue.h"
#include "freertos/event_groups.h"

#include "esp_system.h"
#include "esp_log.h"
#include "esp_event.h"
#include "nvs_flash.h"

#include "driver/gpio.h"

#include "config.h"
#include "wifi.h"
#include "mqtt.h"
#include "button.h"
#include "alert_queue.h"
#include "watchdog.h"
#include "time_sync.h"

static const char *TAG = "MAIN";

/* Event group for synchronization */
static EventGroupHandle_t system_events;

#define WIFI_CONNECTED_BIT  BIT0
#define MQTT_CONNECTED_BIT  BIT1
#define BUTTON_PRESSED_BIT  BIT2

/* Forward declarations */
static void button_task(void *pvParameters);
static void status_task(void *pvParameters);
static void setup_gpio(void);

/**
 * Application entry point
 */
void app_main(void)
{
    ESP_LOGI(TAG, "");
    ESP_LOGI(TAG, "╔═══════════════════════════════════════════════════════════╗");
    ESP_LOGI(TAG, "║   SafeSignal ESP32-S3 Button                              ║");
    ESP_LOGI(TAG, "║   Version: %-47s║", SAFESIGNAL_VERSION);
    ESP_LOGI(TAG, "║   Device ID: %-45s║", DEVICE_ID);
    ESP_LOGI(TAG, "╚═══════════════════════════════════════════════════════════╝");
    ESP_LOGI(TAG, "");

    /* Initialize NVS */
    esp_err_t ret = nvs_flash_init();
    if (ret == ESP_ERR_NVS_NO_FREE_PAGES || ret == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_ERROR_CHECK(nvs_flash_erase());
        ret = nvs_flash_init();
    }
    ESP_ERROR_CHECK(ret);

    /* Initialize event loop */
    ESP_ERROR_CHECK(esp_event_loop_create_default());

    /* Create event group */
    system_events = xEventGroupCreate();

    /* Initialize GPIO (LED, button) */
    setup_gpio();

    /* Initialize alert queue (NVS-based persistence) */
    ESP_ERROR_CHECK(alert_queue_init());

    /* Initialize watchdog */
    ESP_ERROR_CHECK(watchdog_init());

    /* Initialize WiFi */
    wifi_init();

    /* Initialize time synchronization (SNTP) */
    ESP_ERROR_CHECK(time_sync_init());

    /* Initialize MQTT */
    mqtt_init();

    /* Create tasks */
    TaskHandle_t button_task_handle, status_task_handle;
    xTaskCreate(button_task, "button_task", 4096, NULL, 5, &button_task_handle);
    xTaskCreate(status_task, "status_task", 4096, NULL, 3, &status_task_handle);

    /* Register tasks with watchdog */
    ESP_ERROR_CHECK(watchdog_add_task(button_task_handle, "button_task"));
    ESP_ERROR_CHECK(watchdog_add_task(status_task_handle, "status_task"));

    ESP_LOGI(TAG, "[READY] System initialized");
    ESP_LOGI(TAG, "[READY] Press button to trigger alert");
}

/**
 * Setup GPIO pins (LED, button interrupt)
 */
static void setup_gpio(void)
{
    /* LED pin */
    gpio_config_t led_conf = {
        .pin_bit_mask = (1ULL << LED_PIN),
        .mode = GPIO_MODE_OUTPUT,
        .pull_up_en = GPIO_PULLUP_DISABLE,
        .pull_down_en = GPIO_PULLDOWN_DISABLE,
        .intr_type = GPIO_INTR_DISABLE
    };
    gpio_config(&led_conf);
    gpio_set_level(LED_PIN, LED_ACTIVE_HIGH ? 0 : 1);

    /* Button pin with interrupt */
    gpio_config_t button_conf = {
        .pin_bit_mask = (1ULL << BUTTON_PIN),
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_ENABLE,
        .pull_down_en = GPIO_PULLDOWN_DISABLE,
        .intr_type = GPIO_INTR_NEGEDGE  /* Falling edge (button press) */
    };
    gpio_config(&button_conf);

    /* Install GPIO ISR service */
    gpio_install_isr_service(0);

    ESP_LOGI(TAG, "[GPIO] LED configured on GPIO%d", LED_PIN);
    ESP_LOGI(TAG, "[GPIO] Button configured on GPIO%d", BUTTON_PIN);
}

/**
 * Button handling task
 * Monitors button press events and publishes alerts
 */
static void button_task(void *pvParameters)
{
    static uint32_t alerts_sent = 0;
    static uint32_t alerts_failed = 0;

    button_init(system_events, BUTTON_PRESSED_BIT);

    ESP_LOGI(TAG, "[BUTTON] Task started");

    while (1) {
        /* Feed watchdog */
        watchdog_feed();

        /* Wait for button press event */
        EventBits_t bits = xEventGroupWaitBits(
            system_events,
            BUTTON_PRESSED_BIT,
            pdTRUE,   /* Clear bit on exit */
            pdFALSE,  /* Wait for any bit */
            pdMS_TO_TICKS(5000)  /* 5s timeout to feed watchdog periodically */
        );

        if (bits & BUTTON_PRESSED_BIT) {
            ESP_LOGW(TAG, "");
            ESP_LOGW(TAG, "[BUTTON] *** PANIC BUTTON PRESSED ***");

            /* Blink LED rapidly */
            for (int i = 0; i < 5; i++) {
                gpio_set_level(LED_PIN, LED_ACTIVE_HIGH ? 1 : 0);
                vTaskDelay(pdMS_TO_TICKS(100));
                gpio_set_level(LED_PIN, LED_ACTIVE_HIGH ? 0 : 1);
                vTaskDelay(pdMS_TO_TICKS(100));
            }

            /* Publish alert */
            if (mqtt_publish_alert()) {
                alerts_sent++;
                ESP_LOGI(TAG, "[ALERT] ✓ Alert sent (total: %lu)", alerts_sent);
            } else {
                alerts_failed++;
                ESP_LOGE(TAG, "[ALERT] ✗ Alert failed (total failures: %lu)", alerts_failed);
            }

            /* LED solid on */
            gpio_set_level(LED_PIN, LED_ACTIVE_HIGH ? 1 : 0);

            ESP_LOGW(TAG, "");
        }
    }
}

/**
 * Status reporting task
 * Periodically publishes device status and heartbeat
 */
static void status_task(void *pvParameters)
{
    ESP_LOGI(TAG, "[STATUS] Task started");

    /* Wait for MQTT connection */
    xEventGroupWaitBits(system_events, MQTT_CONNECTED_BIT, pdFALSE, pdTRUE, portMAX_DELAY);

    uint32_t status_counter = 0;
    uint32_t heartbeat_counter = 0;

    while (1) {
        /* Feed watchdog */
        watchdog_feed();

        /* Status report every 60 seconds */
        if ((status_counter++ * 1000) >= STATUS_REPORT_INTERVAL_MS) {
            mqtt_publish_status();
            status_counter = 0;
        }

        /* Heartbeat every 30 seconds */
        if ((heartbeat_counter++ * 1000) >= HEARTBEAT_INTERVAL_MS) {
            mqtt_publish_heartbeat();
            heartbeat_counter = 0;
        }

        /* Process queued alerts periodically */
        if ((status_counter % 10) == 0) {  /* Every 10 seconds */
            int pending = alert_queue_get_count();
            if (pending > 0) {
                ESP_LOGI(TAG, "[STATUS] Processing %d queued alerts", pending);
                alert_queue_process();
            }
        }

        vTaskDelay(pdMS_TO_TICKS(1000));
    }
}
