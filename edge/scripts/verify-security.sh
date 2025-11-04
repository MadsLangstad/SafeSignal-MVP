#!/bin/bash
# Security verification script for SafeSignal Edge hardening
# Verifies EMQX mTLS enforcement, ACL isolation, and MinIO TLS

set -e

echo "=================================="
echo "SafeSignal Security Verification"
echo "=================================="
echo ""

# Color codes for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counters
TESTS_PASSED=0
TESTS_FAILED=0

# Helper functions
pass() {
    echo -e "${GREEN}✓ PASS${NC}: $1"
    ((TESTS_PASSED++))
}

fail() {
    echo -e "${RED}✗ FAIL${NC}: $1"
    ((TESTS_FAILED++))
}

warn() {
    echo -e "${YELLOW}⚠ WARN${NC}: $1"
}

# 1. Check EMQX configuration
echo "1. Verifying EMQX Configuration"
echo "--------------------------------"

# Check if EMQX container is running
if docker ps | grep -q safesignal-emqx; then
    pass "EMQX container is running"
else
    fail "EMQX container is not running"
fi

# Check that port 1883 is NOT exposed
if docker ps | grep safesignal-emqx | grep -q "1883"; then
    fail "Port 1883 (unauthenticated) is exposed"
else
    pass "Port 1883 (unauthenticated) is NOT exposed"
fi

# Check that port 8883 IS exposed
if docker ps | grep safesignal-emqx | grep -q "8883"; then
    pass "Port 8883 (mTLS) is exposed"
else
    fail "Port 8883 (mTLS) is NOT exposed"
fi

# Check allow_anonymous in emqx.conf
if grep -q "allow_anonymous = false" edge/emqx/emqx.conf; then
    pass "emqx.conf has allow_anonymous = false"
else
    fail "emqx.conf has allow_anonymous = true (security risk)"
fi

# Check ACL file is mounted
if docker exec safesignal-emqx test -f /opt/emqx/etc/acl.conf; then
    pass "ACL file is mounted in EMQX container"
else
    fail "ACL file is NOT mounted (tenant isolation broken)"
fi

# Verify ACL file format (EMQX 5.x)
if grep -q "{allow, {user," edge/emqx/acl.conf; then
    pass "ACL file uses EMQX 5.x format"
else
    fail "ACL file uses incorrect format"
fi

echo ""

# 2. Check MinIO TLS configuration
echo "2. Verifying MinIO TLS Configuration"
echo "-------------------------------------"

# Check if MinIO container is running
if docker ps | grep -q safesignal-minio; then
    pass "MinIO container is running"
else
    fail "MinIO container is not running"
fi

# Check MinIO certificates are mounted
if docker exec safesignal-minio test -f /root/.minio/certs/public.crt; then
    pass "MinIO server certificate is mounted"
else
    fail "MinIO server certificate is NOT mounted"
fi

if docker exec safesignal-minio test -f /root/.minio/certs/CAs/ca.crt; then
    pass "MinIO CA certificate is mounted"
else
    fail "MinIO CA certificate is NOT mounted"
fi

# Check MinIO is serving HTTPS
if docker exec safesignal-minio curl -k -s -o /dev/null -w "%{http_code}" https://localhost:9000/minio/health/live | grep -q "200"; then
    pass "MinIO is serving HTTPS"
else
    fail "MinIO is NOT serving HTTPS"
fi

echo ""

# 3. Check PA Service configuration
echo "3. Verifying PA Service Configuration"
echo "--------------------------------------"

# Check if PA Service container is running
if docker ps | grep -q safesignal-pa-service; then
    pass "PA Service container is running"
else
    warn "PA Service container is not running (start to test)"
fi

# Check CA certificate installation in PA service
if docker exec safesignal-pa-service test -f /usr/local/share/ca-certificates/safesignal-ca.crt 2>/dev/null; then
    pass "CA certificate is installed in PA service container"
else
    warn "CA certificate is NOT installed in PA service (may cause MinIO TLS errors)"
fi

# Check docker-compose MinIO__UseSSL setting
if grep -q "MinIO__UseSSL=true" edge/docker-compose.yml; then
    pass "PA service configured to use MinIO SSL"
else
    fail "PA service NOT configured to use MinIO SSL"
fi

echo ""

# 4. Test connectivity
echo "4. Testing Service Connectivity"
echo "--------------------------------"

# Test EMQX health
if docker exec safesignal-emqx emqx ping | grep -q "pong"; then
    pass "EMQX is healthy"
else
    fail "EMQX health check failed"
fi

# Test MinIO health
if docker exec safesignal-minio curl -k -s https://localhost:9000/minio/health/live | grep -q "live"; then
    pass "MinIO is healthy"
else
    fail "MinIO health check failed"
fi

# Test PA Service health (if running)
if docker ps | grep -q safesignal-pa-service; then
    if curl -s -f http://localhost:5101/health > /dev/null; then
        pass "PA Service is healthy"
    else
        warn "PA Service health check failed"
    fi
fi

echo ""
echo "=================================="
echo "Test Summary"
echo "=================================="
echo -e "${GREEN}Passed: $TESTS_PASSED${NC}"
echo -e "${RED}Failed: $TESTS_FAILED${NC}"
echo ""

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}All security checks passed!${NC}"
    exit 0
else
    echo -e "${RED}Some security checks failed. Please review the output above.${NC}"
    exit 1
fi
