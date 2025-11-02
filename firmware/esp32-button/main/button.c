#include "button.h"
#include "config.h"

#include "driver/gpio.h"
#include "esp_log.h"

static const char *TAG = "BUTTON";

static EventGroupHandle_t event_group = NULL;
static EventBits_t event_bit = 0;
static uint32_t last_press_time = 0;

/* ISR handler - must be in IRAM */
static void IRAM_ATTR button_isr_handler(void *arg)
{
    uint32_t now = xTaskGetTickCountFromISR() * portTICK_PERIOD_MS;

    /* Debouncing */
    if (now - last_press_time > BUTTON_DEBOUNCE_MS) {
        last_press_time = now;

        /* Set event bit from ISR */
        BaseType_t xHigherPriorityTaskWoken = pdFALSE;
        xEventGroupSetBitsFromISR(event_group, event_bit, &xHigherPriorityTaskWoken);

        if (xHigherPriorityTaskWoken) {
            portYIELD_FROM_ISR();
        }
    }
}

void button_init(EventGroupHandle_t events, EventBits_t press_bit)
{
    event_group = events;
    event_bit = press_bit;

    /* Attach interrupt handler */
    gpio_isr_handler_add(BUTTON_PIN, button_isr_handler, NULL);

    ESP_LOGI(TAG, "[BUTTON] Interrupt handler attached (debounce: %d ms)",
             BUTTON_DEBOUNCE_MS);
}
