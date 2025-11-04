/**
 * SafeSignal Provisioning Console Commands
 *
 * Interactive console commands for device provisioning via UART.
 * Use for manual provisioning, debugging, and factory operations.
 */

#include <stdio.h>
#include <string.h>
#include "esp_log.h"
#include "esp_console.h"
#include "argtable3/argtable3.h"
#include "provisioning.h"

static const char *TAG = "CMD_PROVISION";

/* ========================================================================== */
/* Command: provision_status                                                  */
/* ========================================================================== */

static int cmd_provision_status(int argc, char **argv)
{
    printf("\n");
    printf("╔═══════════════════════════════════════════════════════════╗\n");
    printf("║   SafeSignal Provisioning Status                          ║\n");
    printf("╚═══════════════════════════════════════════════════════════╝\n");
    printf("\n");

    bool provisioned = provision_is_provisioned();
    printf("Status: %s\n", provisioned ? "PROVISIONED ✓" : "NOT PROVISIONED ✗");
    printf("\n");

    if (provisioned) {
        device_config_t config;
        esp_err_t err = provision_load_config(&config);

        if (err == ESP_OK) {
            printf("Configuration:\n");
            printf("  WiFi SSID:    %s\n", config.wifi_ssid);
            printf("  WiFi Pass:    [HIDDEN]\n");
            printf("  Device ID:    %s\n", config.device_id);
            printf("  Tenant ID:    %s\n", config.tenant_id);
            printf("  Building ID:  %s\n", config.building_id);
            printf("  Room ID:      %s\n", config.room_id);
            printf("\n");
        } else {
            printf("Error loading configuration: %s\n", esp_err_to_name(err));
        }

        /* Check for certificates */
        device_certs_t certs = {0};
        err = provision_load_certificates(&certs);
        if (err == ESP_OK) {
            printf("Certificates: PRESENT ✓\n");
            provision_free_certificates(&certs);
        } else if (err == ESP_ERR_NVS_NOT_FOUND) {
            printf("Certificates: NOT CONFIGURED\n");
        } else {
            printf("Certificates: ERROR (%s)\n", esp_err_to_name(err));
        }
    }

    printf("\n");
    return 0;
}

static void register_provision_status(void)
{
    const esp_console_cmd_t cmd = {
        .command = "provision_status",
        .help = "Show device provisioning status",
        .hint = NULL,
        .func = &cmd_provision_status,
    };
    ESP_ERROR_CHECK(esp_console_cmd_register(&cmd));
}

/* ========================================================================== */
/* Command: provision_set_wifi                                                */
/* ========================================================================== */

static struct {
    struct arg_str *ssid;
    struct arg_str *password;
    struct arg_end *end;
} provision_wifi_args;

static int cmd_provision_set_wifi(int argc, char **argv)
{
    int nerrors = arg_parse(argc, argv, (void **)&provision_wifi_args);
    if (nerrors != 0) {
        arg_print_errors(stderr, provision_wifi_args.end, argv[0]);
        return 1;
    }

    const char *ssid = provision_wifi_args.ssid->sval[0];
    const char *password = provision_wifi_args.password->sval[0];

    /* Validate inputs */
    if (strlen(ssid) == 0 || strlen(ssid) >= MAX_WIFI_SSID_LEN) {
        printf("Error: WiFi SSID must be 1-%d characters\n", MAX_WIFI_SSID_LEN - 1);
        return 1;
    }
    if (strlen(password) >= MAX_WIFI_PASS_LEN) {
        printf("Error: WiFi password must be less than %d characters\n", MAX_WIFI_PASS_LEN);
        return 1;
    }

    /* Save WiFi credentials */
    esp_err_t err;
    err = provision_set_string(PROVISION_KEY_WIFI_SSID, ssid);
    if (err != ESP_OK) {
        printf("Error saving WiFi SSID: %s\n", esp_err_to_name(err));
        return 1;
    }

    err = provision_set_string(PROVISION_KEY_WIFI_PASS, password);
    if (err != ESP_OK) {
        printf("Error saving WiFi password: %s\n", esp_err_to_name(err));
        return 1;
    }

    printf("WiFi credentials saved: SSID='%s'\n", ssid);
    printf("Note: Reboot required for changes to take effect\n");
    return 0;
}

static void register_provision_set_wifi(void)
{
    provision_wifi_args.ssid = arg_str1(NULL, NULL, "<ssid>", "WiFi SSID");
    provision_wifi_args.password = arg_str1(NULL, NULL, "<password>", "WiFi password");
    provision_wifi_args.end = arg_end(2);

    const esp_console_cmd_t cmd = {
        .command = "provision_set_wifi",
        .help = "Configure WiFi credentials",
        .hint = NULL,
        .func = &cmd_provision_set_wifi,
        .argtable = &provision_wifi_args
    };
    ESP_ERROR_CHECK(esp_console_cmd_register(&cmd));
}

/* ========================================================================== */
/* Command: provision_set_device                                              */
/* ========================================================================== */

static struct {
    struct arg_str *device_id;
    struct arg_str *tenant_id;
    struct arg_str *building_id;
    struct arg_str *room_id;
    struct arg_end *end;
} provision_device_args;

static int cmd_provision_set_device(int argc, char **argv)
{
    int nerrors = arg_parse(argc, argv, (void **)&provision_device_args);
    if (nerrors != 0) {
        arg_print_errors(stderr, provision_device_args.end, argv[0]);
        return 1;
    }

    const char *device_id = provision_device_args.device_id->sval[0];
    const char *tenant_id = provision_device_args.tenant_id->sval[0];
    const char *building_id = provision_device_args.building_id->sval[0];
    const char *room_id = provision_device_args.room_id->sval[0];

    /* Validate inputs */
    if (strlen(device_id) == 0 || strlen(device_id) >= MAX_DEVICE_ID_LEN) {
        printf("Error: Device ID must be 1-%d characters\n", MAX_DEVICE_ID_LEN - 1);
        return 1;
    }
    if (strlen(tenant_id) >= MAX_TENANT_ID_LEN) {
        printf("Error: Tenant ID must be less than %d characters\n", MAX_TENANT_ID_LEN);
        return 1;
    }
    if (strlen(building_id) >= MAX_BUILDING_ID_LEN) {
        printf("Error: Building ID must be less than %d characters\n", MAX_BUILDING_ID_LEN);
        return 1;
    }
    if (strlen(room_id) >= MAX_ROOM_ID_LEN) {
        printf("Error: Room ID must be less than %d characters\n", MAX_ROOM_ID_LEN);
        return 1;
    }

    /* Save device configuration */
    esp_err_t err;
    err = provision_set_string(PROVISION_KEY_DEVICE_ID, device_id);
    if (err != ESP_OK) {
        printf("Error saving device ID: %s\n", esp_err_to_name(err));
        return 1;
    }

    err = provision_set_string(PROVISION_KEY_TENANT_ID, tenant_id);
    if (err != ESP_OK) {
        printf("Error saving tenant ID: %s\n", esp_err_to_name(err));
        return 1;
    }

    err = provision_set_string(PROVISION_KEY_BUILDING_ID, building_id);
    if (err != ESP_OK) {
        printf("Error saving building ID: %s\n", esp_err_to_name(err));
        return 1;
    }

    err = provision_set_string(PROVISION_KEY_ROOM_ID, room_id);
    if (err != ESP_OK) {
        printf("Error saving room ID: %s\n", esp_err_to_name(err));
        return 1;
    }

    printf("Device configuration saved:\n");
    printf("  Device ID:   %s\n", device_id);
    printf("  Tenant ID:   %s\n", tenant_id);
    printf("  Building ID: %s\n", building_id);
    printf("  Room ID:     %s\n", room_id);
    printf("Note: Reboot required for changes to take effect\n");
    return 0;
}

static void register_provision_set_device(void)
{
    provision_device_args.device_id = arg_str1(NULL, NULL, "<device_id>", "Device ID");
    provision_device_args.tenant_id = arg_str1(NULL, NULL, "<tenant_id>", "Tenant ID");
    provision_device_args.building_id = arg_str1(NULL, NULL, "<building_id>", "Building ID");
    provision_device_args.room_id = arg_str1(NULL, NULL, "<room_id>", "Room ID");
    provision_device_args.end = arg_end(4);

    const esp_console_cmd_t cmd = {
        .command = "provision_set_device",
        .help = "Configure device metadata",
        .hint = NULL,
        .func = &cmd_provision_set_device,
        .argtable = &provision_device_args
    };
    ESP_ERROR_CHECK(esp_console_cmd_register(&cmd));
}

/* ========================================================================== */
/* Command: provision_complete                                                */
/* ========================================================================== */

static int cmd_provision_complete(int argc, char **argv)
{
    /* Verify minimum required configuration */
    char test_buf[64];

    esp_err_t err = provision_get_string(PROVISION_KEY_WIFI_SSID, test_buf, sizeof(test_buf));
    if (err != ESP_OK) {
        printf("Error: WiFi SSID not configured\n");
        printf("Use: provision_set_wifi <ssid> <password>\n");
        return 1;
    }

    err = provision_get_string(PROVISION_KEY_DEVICE_ID, test_buf, sizeof(test_buf));
    if (err != ESP_OK) {
        printf("Error: Device ID not configured\n");
        printf("Use: provision_set_device <device_id> <tenant_id> <building_id> <room_id>\n");
        return 1;
    }

    /* Mark as provisioned */
    err = provision_mark_provisioned();
    if (err != ESP_OK) {
        printf("Error marking device as provisioned: %s\n", esp_err_to_name(err));
        return 1;
    }

    printf("\n");
    printf("╔═══════════════════════════════════════════════════════════╗\n");
    printf("║   Device Provisioning Complete!                           ║\n");
    printf("╚═══════════════════════════════════════════════════════════╝\n");
    printf("\n");
    printf("Device is now provisioned and will use stored credentials.\n");
    printf("Reboot the device for changes to take effect.\n");
    printf("\n");

    return 0;
}

static void register_provision_complete(void)
{
    const esp_console_cmd_t cmd = {
        .command = "provision_complete",
        .help = "Mark provisioning as complete",
        .hint = NULL,
        .func = &cmd_provision_complete,
    };
    ESP_ERROR_CHECK(esp_console_cmd_register(&cmd));
}

/* ========================================================================== */
/* Command: provision_reset (factory reset)                                   */
/* ========================================================================== */

static struct {
    struct arg_lit *confirm;
    struct arg_end *end;
} provision_reset_args;

static int cmd_provision_reset(int argc, char **argv)
{
    int nerrors = arg_parse(argc, argv, (void **)&provision_reset_args);
    if (nerrors != 0) {
        arg_print_errors(stderr, provision_reset_args.end, argv[0]);
        return 1;
    }

    if (!provision_reset_args.confirm->count) {
        printf("Warning: This will erase all provisioning data!\n");
        printf("Use: provision_reset --confirm\n");
        return 1;
    }

    esp_err_t err = provision_clear();
    if (err != ESP_OK) {
        printf("Error clearing provisioning data: %s\n", esp_err_to_name(err));
        return 1;
    }

    printf("\n");
    printf("╔═══════════════════════════════════════════════════════════╗\n");
    printf("║   Factory Reset Complete                                  ║\n");
    printf("╚═══════════════════════════════════════════════════════════╝\n");
    printf("\n");
    printf("All provisioning data has been erased.\n");
    printf("Device will require reprovisioning on next boot.\n");
    printf("\n");

    return 0;
}

static void register_provision_reset(void)
{
    provision_reset_args.confirm = arg_lit0(NULL, "confirm", "Confirm factory reset");
    provision_reset_args.end = arg_end(1);

    const esp_console_cmd_t cmd = {
        .command = "provision_reset",
        .help = "Factory reset (erase all provisioning data)",
        .hint = NULL,
        .func = &cmd_provision_reset,
        .argtable = &provision_reset_args
    };
    ESP_ERROR_CHECK(esp_console_cmd_register(&cmd));
}

/* ========================================================================== */
/* Command: provision_get                                                     */
/* ========================================================================== */

static struct {
    struct arg_str *key;
    struct arg_end *end;
} provision_get_args;

static int cmd_provision_get(int argc, char **argv)
{
    int nerrors = arg_parse(argc, argv, (void **)&provision_get_args);
    if (nerrors != 0) {
        arg_print_errors(stderr, provision_get_args.end, argv[0]);
        return 1;
    }

    const char *key = provision_get_args.key->sval[0];
    char value[256];

    esp_err_t err = provision_get_string(key, value, sizeof(value));
    if (err == ESP_OK) {
        /* Hide sensitive values */
        if (strcmp(key, PROVISION_KEY_WIFI_PASS) == 0 ||
            strstr(key, "key") != NULL ||
            strstr(key, "cert") != NULL) {
            printf("%s: [HIDDEN]\n", key);
        } else {
            printf("%s: %s\n", key, value);
        }
    } else {
        printf("Error reading '%s': %s\n", key, esp_err_to_name(err));
        return 1;
    }

    return 0;
}

static void register_provision_get(void)
{
    provision_get_args.key = arg_str1(NULL, NULL, "<key>", "Configuration key");
    provision_get_args.end = arg_end(1);

    const esp_console_cmd_t cmd = {
        .command = "provision_get",
        .help = "Get provisioning value by key",
        .hint = NULL,
        .func = &cmd_provision_get,
        .argtable = &provision_get_args
    };
    ESP_ERROR_CHECK(esp_console_cmd_register(&cmd));
}

/* ========================================================================== */
/* Command: provision_set_cert                                                */
/* ========================================================================== */

static struct {
    struct arg_str *cert_type;
    struct arg_str *cert_data;
    struct arg_end *end;
} provision_cert_args;

static int cmd_provision_set_cert(int argc, char **argv)
{
    int nerrors = arg_parse(argc, argv, (void **)&provision_cert_args);
    if (nerrors != 0) {
        arg_print_errors(stderr, provision_cert_args.end, argv[0]);
        return 1;
    }

    const char *cert_type = provision_cert_args.cert_type->sval[0];
    const char *cert_data = provision_cert_args.cert_data->sval[0];

    /* Map type to NVS key */
    const char *key = NULL;
    if (strcmp(cert_type, "ca") == 0) {
        key = PROVISION_KEY_CA_CERT;
    } else if (strcmp(cert_type, "client") == 0) {
        key = PROVISION_KEY_CLIENT_CERT;
    } else if (strcmp(cert_type, "key") == 0) {
        key = PROVISION_KEY_CLIENT_KEY;
    } else {
        printf("Error: Invalid certificate type. Use: ca, client, or key\n");
        return 1;
    }

    /* Validate certificate data (basic PEM format check) */
    if (strstr(cert_data, "-----BEGIN") == NULL) {
        printf("Warning: Certificate data doesn't appear to be in PEM format\n");
        printf("Expected format: -----BEGIN CERTIFICATE----- ... -----END CERTIFICATE-----\n");
        printf("Continue anyway? (y/n): ");

        char confirm;
        scanf(" %c", &confirm);
        if (confirm != 'y' && confirm != 'Y') {
            printf("Certificate not saved\n");
            return 1;
        }
    }

    /* Validate size */
    size_t cert_len = strlen(cert_data);
    if (cert_len >= MAX_CERT_LEN) {
        printf("Error: Certificate too large (%zu bytes, max %d bytes)\n",
               cert_len, MAX_CERT_LEN);
        return 1;
    }

    /* Save certificate */
    esp_err_t err = provision_set_string(key, cert_data);
    if (err != ESP_OK) {
        printf("Error saving certificate: %s\n", esp_err_to_name(err));
        return 1;
    }

    printf("Certificate '%s' saved successfully (%zu bytes)\n", cert_type, cert_len);
    printf("Note: Use 'provision_load_cert_file' to load from file instead\n");
    return 0;
}

static void register_provision_set_cert(void)
{
    provision_cert_args.cert_type = arg_str1(NULL, NULL, "<type>",
        "Certificate type: ca, client, or key");
    provision_cert_args.cert_data = arg_str1(NULL, NULL, "<data>",
        "Certificate data (PEM format, in quotes)");
    provision_cert_args.end = arg_end(2);

    const esp_console_cmd_t cmd = {
        .command = "provision_set_cert",
        .help = "Set certificate (ca/client/key) - for testing only, use file loading in production",
        .hint = NULL,
        .func = &cmd_provision_set_cert,
        .argtable = &provision_cert_args
    };
    ESP_ERROR_CHECK(esp_console_cmd_register(&cmd));
}

/* ========================================================================== */
/* Command: provision_cert_status                                             */
/* ========================================================================== */

static int cmd_provision_cert_status(int argc, char **argv)
{
    printf("\n");
    printf("Certificate Status:\n");
    printf("------------------\n");

    char test_buf[64];
    esp_err_t err;

    /* Check CA certificate */
    err = provision_get_string(PROVISION_KEY_CA_CERT, test_buf, sizeof(test_buf));
    if (err == ESP_OK) {
        printf("  CA Certificate:     PRESENT ✓\n");
    } else if (err == ESP_ERR_NVS_NOT_FOUND) {
        printf("  CA Certificate:     NOT CONFIGURED ✗\n");
    } else {
        printf("  CA Certificate:     ERROR (%s)\n", esp_err_to_name(err));
    }

    /* Check client certificate */
    err = provision_get_string(PROVISION_KEY_CLIENT_CERT, test_buf, sizeof(test_buf));
    if (err == ESP_OK) {
        printf("  Client Certificate: PRESENT ✓\n");
    } else if (err == ESP_ERR_NVS_NOT_FOUND) {
        printf("  Client Certificate: NOT CONFIGURED ✗\n");
    } else {
        printf("  Client Certificate: ERROR (%s)\n", esp_err_to_name(err));
    }

    /* Check client key */
    err = provision_get_string(PROVISION_KEY_CLIENT_KEY, test_buf, sizeof(test_buf));
    if (err == ESP_OK) {
        printf("  Client Key:         PRESENT ✓\n");
    } else if (err == ESP_ERR_NVS_NOT_FOUND) {
        printf("  Client Key:         NOT CONFIGURED ✗\n");
    } else {
        printf("  Client Key:         ERROR (%s)\n", esp_err_to_name(err));
    }

    printf("\n");
    printf("Note: Certificates are optional. Device will use embedded certificates\n");
    printf("      if not provisioned. Provision certificates for production use.\n");
    printf("\n");

    return 0;
}

static void register_provision_cert_status(void)
{
    const esp_console_cmd_t cmd = {
        .command = "provision_cert_status",
        .help = "Show certificate provisioning status",
        .hint = NULL,
        .func = &cmd_provision_cert_status,
    };
    ESP_ERROR_CHECK(esp_console_cmd_register(&cmd));
}

/* ========================================================================== */
/* Public API: Register all provisioning commands                             */
/* ========================================================================== */

void register_provision_commands(void)
{
    ESP_LOGI(TAG, "Registering provisioning console commands");

    register_provision_status();
    register_provision_set_wifi();
    register_provision_set_device();
    register_provision_complete();
    register_provision_reset();
    register_provision_get();
    register_provision_set_cert();
    register_provision_cert_status();
}
