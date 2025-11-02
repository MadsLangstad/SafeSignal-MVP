# SafeSignal Certificates

Place your mTLS certificates here before building firmware.

## Required Files

- `ca.crt` - Certificate Authority certificate
- `client.crt` - Device client certificate
- `client.key` - Device private key

## Development Setup

For development, copy certificates from the edge gateway:

```bash
cp ../../edge/certs/ca/ca.crt ./ca.crt
cp ../../edge/certs/devices/esp32-test.crt ./client.crt
cp ../../edge/certs/devices/esp32-test.key ./client.key
```

## Production Deployment

For production, certificates should be:
1. Generated per-device with unique device IDs
2. Stored securely with restricted access
3. Rotated regularly (via SPIFFE/SPIRE in production)
4. Private keys should be stored in ATECC608A secure element

**Never commit real certificates to version control!**
