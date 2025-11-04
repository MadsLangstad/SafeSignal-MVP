/**
 * SafeSignal Provisioning Console Commands
 * Header file
 */

#ifndef SAFESIGNAL_CMD_PROVISION_H
#define SAFESIGNAL_CMD_PROVISION_H

/**
 * @brief Register all provisioning console commands
 *
 * Registers the following commands:
 * - provision_status: Show provisioning status
 * - provision_set_wifi: Configure WiFi credentials
 * - provision_set_device: Configure device metadata
 * - provision_complete: Mark provisioning as complete
 * - provision_reset: Factory reset (erase all provisioning data)
 * - provision_get: Get provisioning value by key
 */
void register_provision_commands(void);

#endif /* SAFESIGNAL_CMD_PROVISION_H */
