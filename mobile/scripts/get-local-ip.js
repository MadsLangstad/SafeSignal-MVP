#!/usr/bin/env node

/**
 * Auto-detect local network IP address for mobile app development
 * This script finds the machine's local network IP automatically
 */

const os = require('os');

function getLocalIpAddress() {
  const interfaces = os.networkInterfaces();

  // Priority order: en0 (WiFi), en1 (Ethernet), others
  const priorityInterfaces = ['en0', 'en1', 'eth0', 'wlan0'];

  // Try priority interfaces first
  for (const name of priorityInterfaces) {
    if (interfaces[name]) {
      const addresses = interfaces[name].filter(
        (iface) => iface.family === 'IPv4' && !iface.internal
      );
      if (addresses.length > 0) {
        return addresses[0].address;
      }
    }
  }

  // Fallback: try all interfaces
  for (const name of Object.keys(interfaces)) {
    const addresses = interfaces[name].filter(
      (iface) => iface.family === 'IPv4' && !iface.internal
    );
    if (addresses.length > 0) {
      return addresses[0].address;
    }
  }

  // Last resort fallback
  return 'localhost';
}

const ip = getLocalIpAddress();
console.log(ip);
