#ifndef SAFESIGNAL_BUTTON_H
#define SAFESIGNAL_BUTTON_H

#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"

/**
 * Initialize button with interrupt handler
 * @param events Event group for signaling button press
 * @param press_bit Bit to set when button is pressed
 */
void button_init(EventGroupHandle_t events, EventBits_t press_bit);

#endif /* SAFESIGNAL_BUTTON_H */
