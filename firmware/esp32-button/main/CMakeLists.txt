# Main component CMakeLists.txt

# Source files
set(COMPONENT_SRCS
    "main.c"
    "wifi.c"
    "mqtt.c"
    "button.c"
    "alert_queue.c"
    "watchdog.c"
    "time_sync.c"
)

# Include directories
set(COMPONENT_ADD_INCLUDEDIRS
    "."
    "../include"
)

# Embed certificates as binary data
set(COMPONENT_EMBED_TXTFILES
    "../certs/ca.crt"
    "../certs/client.crt"
    "../certs/client.key"
)

# Register component
idf_component_register(
    SRCS ${COMPONENT_SRCS}
    INCLUDE_DIRS ${COMPONENT_ADD_INCLUDEDIRS}
    EMBED_TXTFILES ${COMPONENT_EMBED_TXTFILES}
)
