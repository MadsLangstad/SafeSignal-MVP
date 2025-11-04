#!/usr/bin/env bash
# seed-certs.sh - Generate development mTLS certificates for SafeSignal Edge
#
# Creates a local CA and generates certificates for:
# - EMQX broker (server cert)
# - Policy service (client cert)
# - PA service (client cert)
# - Test ESP32 device (client cert)
# - Test mobile app (client cert)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
CERTS_DIR="${ROOT_DIR}/edge/certs"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
if ! command -v openssl &> /dev/null; then
    log_error "openssl is required but not installed"
    exit 1
fi

# Clean and create certs directory
log_info "Setting up certificates directory..."
rm -rf "$CERTS_DIR"
mkdir -p "$CERTS_DIR"/{ca,emqx,policy-service,pa-service,devices,minio}
cd "$CERTS_DIR"

# Generate CA private key and certificate
log_info "Generating Certificate Authority (CA)..."
openssl genrsa -out ca/ca.key 4096
openssl req -new -x509 -days 3650 -key ca/ca.key -out ca/ca.crt \
    -subj "/C=NO/ST=Oslo/L=Oslo/O=SafeSignal-Dev/OU=Edge-CA/CN=SafeSignal Dev CA"

log_info "CA certificate generated: $(openssl x509 -in ca/ca.crt -noout -subject)"

# Function to generate a certificate
generate_cert() {
    local name=$1
    local cn=$2
    local out_dir=$3
    local cert_type=$4  # "server" or "client"

    log_info "Generating certificate for: $cn"

    # Generate private key
    openssl genrsa -out "${out_dir}/${name}.key" 2048

    # Create CSR config
    cat > "${out_dir}/${name}.cnf" <<EOF
[req]
default_bits = 2048
prompt = no
default_md = sha256
distinguished_name = dn
req_extensions = v3_req

[dn]
C=NO
ST=Oslo
L=Oslo
O=SafeSignal-Dev
OU=Edge
CN=$cn

[v3_req]
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = $(if [ "$cert_type" = "server" ]; then echo "serverAuth"; else echo "clientAuth"; fi)
subjectAltName = @alt_names

[alt_names]
DNS.1 = $cn
DNS.2 = localhost
IP.1 = 127.0.0.1
EOF

    # Generate CSR
    openssl req -new -key "${out_dir}/${name}.key" -out "${out_dir}/${name}.csr" \
        -config "${out_dir}/${name}.cnf"

    # Sign certificate with CA
    openssl x509 -req -in "${out_dir}/${name}.csr" -CA ca/ca.crt -CAkey ca/ca.key \
        -CAcreateserial -out "${out_dir}/${name}.crt" -days 365 \
        -extensions v3_req -extfile "${out_dir}/${name}.cnf"

    # Verify certificate
    openssl verify -CAfile ca/ca.crt "${out_dir}/${name}.crt" > /dev/null

    log_info "✓ Certificate generated and verified: ${out_dir}/${name}.crt"
}

# Generate EMQX broker certificate (server)
generate_cert "server" "emqx" "emqx" "server"

# Generate Policy Service certificate (client)
generate_cert "client" "policy-service" "policy-service" "client"

# Generate PA Service certificate (client)
generate_cert "client" "pa-service" "pa-service" "client"

# Generate test device certificates
log_info "Generating test device certificates..."
generate_cert "esp32-test" "device-esp32-001" "devices" "client"
generate_cert "app-test" "device-app-001" "devices" "client"

# Generate MinIO server certificate
log_info "Generating MinIO server certificate..."
generate_cert "minio-server" "minio" "minio" "server"

# Create combined PEM files for convenience
log_info "Creating combined certificate files..."
cat emqx/server.crt emqx/server.key > emqx/server-combined.pem
cat policy-service/client.crt policy-service/client.key > policy-service/client-combined.pem
cat pa-service/client.crt pa-service/client.key > pa-service/client-combined.pem

# Set appropriate permissions
chmod 600 ca/ca.key
chmod 600 *//*.key
chmod 644 ca/ca.crt
chmod 644 *//*.crt

# Generate certificate inventory
cat > cert-inventory.txt <<EOF
SafeSignal Edge Development Certificates
Generated: $(date -u +"%Y-%m-%d %H:%M:%S UTC")

CA Certificate:
  Location: ca/ca.crt
  Subject: $(openssl x509 -in ca/ca.crt -noout -subject)
  Valid until: $(openssl x509 -in ca/ca.crt -noout -enddate | cut -d= -f2)

EMQX Broker (Server):
  Cert: emqx/server.crt
  Key: emqx/server.key
  Combined: emqx/server-combined.pem
  Subject: $(openssl x509 -in emqx/server.crt -noout -subject)

Policy Service (Client):
  Cert: policy-service/client.crt
  Key: policy-service/client.key
  Subject: $(openssl x509 -in policy-service/client.crt -noout -subject)

PA Service (Client):
  Cert: pa-service/client.crt
  Key: pa-service/client.key
  Subject: $(openssl x509 -in pa-service/client.crt -noout -subject)

Test Devices:
  ESP32: devices/esp32-test.crt / devices/esp32-test.key
  App: devices/app-test.crt / devices/app-test.key

⚠️  WARNING: These are DEVELOPMENT certificates only.
    DO NOT use in production environments.
    All certificates expire in 1 year, CA in 10 years.
EOF

log_info "Certificate inventory written to: cert-inventory.txt"

# Display summary
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
log_info "✓ Certificate generation complete!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Certificates location: $CERTS_DIR"
echo ""
echo "Quick verification:"
echo "  CA:             openssl x509 -in ca/ca.crt -noout -text"
echo "  EMQX Server:    openssl x509 -in emqx/server.crt -noout -text"
echo "  Policy Client:  openssl x509 -in policy-service/client.crt -noout -text"
echo ""
log_warn "These are DEVELOPMENT certificates. Do not use in production!"
echo ""
