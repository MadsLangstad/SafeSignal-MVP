#!/bin/bash
set -e

# Install custom CA certificate if it exists
if [ -f /certs/ca/ca.crt ]; then
    echo "Installing SafeSignal CA certificate..."
    cp /certs/ca/ca.crt /usr/local/share/ca-certificates/safesignal-ca.crt
    update-ca-certificates
    echo "CA certificate installed successfully"
else
    echo "Warning: CA certificate not found at /certs/ca/ca.crt"
fi

# Execute the original entrypoint
exec dotnet PaService.dll "$@"
