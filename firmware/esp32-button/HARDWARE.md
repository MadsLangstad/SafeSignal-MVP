# SafeSignal ESP32-S3 Button Hardware Specification

## Bill of Materials (BOM)

### Development/Prototype Phase

| Component | Part Number | Quantity | Unit Cost | Total | Supplier | Notes |
|-----------|-------------|----------|-----------|-------|----------|-------|
| **Microcontroller** | ESP32-S3-DevKitC-1-N8R8 | 1 | $12.95 | $12.95 | Adafruit, SparkFun, Mouser | 8MB flash, 8MB PSRAM |
| **Secure Element** | ATECC608A-MAHDA-T | 1 | $0.87 | $0.87 | Digi-Key, Mouser | I2C crypto chip |
| **Push Button** | TL1105SPF250Q | 1 | $0.35 | $0.35 | Digi-Key | 12mm tactile switch |
| **LED** | LTST-C193KGKT-5A | 1 | $0.15 | $0.15 | Digi-Key | Green 0805 SMD |
| **Resistor (LED)** | RC0805JR-0710KL | 1 | $0.01 | $0.01 | Digi-Key | 10kΩ 0805 |
| **Resistor (pullup)** | RC0805FR-0710KL | 1 | $0.01 | $0.01 | Digi-Key | 10kΩ 0805 |
| **Enclosure** | 1591XXSSBK | 1 | $8.50 | $8.50 | Hammond | 85x56x25mm ABS |
| **USB Cable** | USB-A to USB-C | 1 | $4.00 | $4.00 | Various | For power and programming |
| **Mounting Hardware** | M3 screws, standoffs | 1 set | $2.00 | $2.00 | Various | Board mounting |

**Subtotal (Development Unit)**: ~$28.84

**Note**: Development units use ESP32-S3-DevKitC-1 for rapid prototyping. Production units will use custom PCB (see below).

---

### Production Phase (Custom PCB)

| Component | Part Number | Quantity | Unit Cost (1K qty) | Notes |
|-----------|-------------|----------|-------------------|-------|
| **ESP32-S3 Module** | ESP32-S3-WROOM-1-N8R8 | 1 | $3.50 | Integrated module |
| **ATECC608A** | ATECC608A-MAHDA-T | 1 | $0.65 | I2C crypto |
| **Push Button** | TL1105SPF250Q | 1 | $0.25 | Tactile switch |
| **LED (Status)** | LTST-C193KGKT-5A | 1 | $0.10 | Green LED |
| **LED (Alert)** | LTST-C193KRKT-5A | 1 | $0.10 | Red LED |
| **Resistors** | Various 0805 | 5 | $0.05 | Pullups, LED current |
| **Capacitors** | Various 0805 | 4 | $0.08 | Decoupling |
| **Voltage Regulator** | AMS1117-3.3 | 1 | $0.25 | 3.3V LDO |
| **USB Connector** | USB-C 2.0 | 1 | $0.50 | Power + programming |
| **PCB** | Custom 2-layer | 1 | $2.00 | 50x50mm |
| **Enclosure** | Custom injection molded | 1 | $3.00 | ABS, red emergency color |
| **Assembly** | SMT + THT | 1 | $2.50 | Pick-and-place + manual |

**Target Unit Cost (1K qty)**: ~$12.98
**Target Unit Cost (10K qty)**: ~$9.50

---

## Hardware Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    ESP32-S3 Module                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                  Dual-Core CPU                       │   │
│  │  - Xtensa LX7 @ 240MHz                               │   │
│  │  - 512KB SRAM, 8MB Flash, 8MB PSRAM                  │   │
│  └──────────────────────────────────────────────────────┘   │
│                         │                                    │
│  ┌─────────┬────────────┼────────────┬─────────┐           │
│  │         │            │            │         │           │
│  ▼         ▼            ▼            ▼         ▼           │
│ GPIO0    GPIO2       GPIO21       GPIO22    USB-C         │
│  │         │            │            │         │           │
└──┼─────────┼────────────┼────────────┼─────────┼───────────┘
   │         │            │            │         │
   ▼         ▼            │            │         ▼
 Button    LED           I2C         I2C     Power (5V)
   │         │            │            │      Programming
   │         │            │            │
   └─────────┘            └────────────┼────────────────┐
                                       │                │
                                       ▼                ▼
                               ┌──────────────┐  ┌──────────────┐
                               │  ATECC608A   │  │  ATECC608A   │
                               │ (SDA: GPIO21)│  │ (SCL: GPIO22)│
                               │              │  │              │
                               │ - Private key│  │ - Device ID  │
                               │ - Device cert│  │ - Signatures │
                               └──────────────┘  └──────────────┘
```

---

## Pin Assignment (ESP32-S3)

| GPIO | Function | Direction | Notes |
|------|----------|-----------|-------|
| GPIO0 | Button Input | Input | Internal pullup, active low |
| GPIO2 | Status LED | Output | Green LED (ready/activity) |
| GPIO3 | Alert LED | Output | Red LED (alert triggered) - future |
| GPIO21 | I2C SDA | I/O | ATECC608A communication |
| GPIO22 | I2C SCL | Output | ATECC608A clock |
| GPIO43 (U0TXD) | UART TX | Output | Debug/programming |
| GPIO44 (U0RXD) | UART RX | Input | Debug/programming |
| USB D+/D- | USB Serial | I/O | Programming and power |

**Reserved for future:**
- GPIO4-7: Additional buttons (multi-mode support)
- GPIO8: Battery voltage ADC
- GPIO9-10: External tamper detection

---

## Power Requirements

### Power Supply Options

1. **USB-C (Primary)**
   - Input: 5V DC via USB-C
   - Regulation: AMS1117-3.3 → 3.3V for ESP32-S3
   - Current: 500mA max (USB 2.0 spec)

2. **Battery Backup (Future)**
   - 18650 Li-ion cell (3.7V, 2600mAh)
   - TP4056 charging circuit
   - UPS functionality: Switch to battery on power loss
   - Runtime: ~10 hours (active), ~72 hours (deep sleep)

### Power Consumption

| Mode | Current | Power (3.3V) | Notes |
|------|---------|--------------|-------|
| **Active (WiFi TX)** | 280mA | 924mW | Peak during MQTT publish |
| **Active (WiFi RX)** | 95mA | 314mW | Listening for messages |
| **Idle (Connected)** | 60mA | 198mW | WiFi connected, no activity |
| **Modem Sleep** | 15mA | 50mW | WiFi off, wakes on timer |
| **Light Sleep** | 800µA | 2.64mW | CPU paused, RAM retained |
| **Deep Sleep** | 10µA | 33µW | Only ULP coprocessor active |

**Typical Average (Wall-powered)**: ~80mA @ 3.3V = 264mW

---

## Mechanical Specifications

### Enclosure (Prototype - Hammond 1591XXSSBK)
- **Dimensions**: 85mm (L) × 56mm (W) × 25mm (H)
- **Material**: ABS plastic, black
- **Mounting**: 4x M3 screw holes
- **Protection**: IP54 (dust and splash resistant)
- **Color**: Red (emergency button standard)

### Button (TL1105SPF250Q)
- **Type**: Tactile momentary pushbutton
- **Size**: 12mm × 12mm × 7.3mm
- **Actuation Force**: 250gf (medium tactile)
- **Travel**: 0.5mm
- **Life**: 1,000,000 cycles
- **Feel**: Positive tactile feedback

### Production Enclosure (Custom)
- **Design**: Red injection-molded ABS
- **Button**: Large flush-mount (30mm diameter)
- **Labeling**: "EMERGENCY" embossed text
- **Mounting**: Wall-mount or desk-mount options
- **Tamper**: Optional tamper switch on back
- **Compliance**: UL94 V-0 flame rating

---

## Environmental Specifications

| Parameter | Specification | Notes |
|-----------|---------------|-------|
| **Operating Temperature** | 0°C to 50°C | Indoor use |
| **Storage Temperature** | -20°C to 70°C | |
| **Humidity** | 10% to 90% RH | Non-condensing |
| **Altitude** | Up to 2000m | |
| **Shock** | MIL-STD-810G | Drop-resistant enclosure |
| **Vibration** | IEC 60068-2-6 | Secure mounting required |

---

## Certifications Required

### Radio Compliance
- **FCC Part 15** (USA) - Radio frequency emissions
- **CE/RED** (Europe) - Radio Equipment Directive
- **IC** (Canada) - Industry Canada certification
- **Wi-Fi Alliance** - Wi-Fi CERTIFIED

### Safety
- **UL 60950-1** - Electrical safety
- **IEC 62368-1** - Audio/video equipment safety

### EMC
- **FCC Part 15B** - Electromagnetic compatibility
- **EN 55032** - EMC emissions
- **EN 55035** - EMC immunity

---

## Schematic Highlights

### Power Section
```
USB-C (5V) → AMS1117-3.3 (3.3V) → ESP32-S3
                                 → ATECC608A
                                 → LEDs (via current-limiting resistors)
```

### Button Input
```
GPIO0 ──────┬──── Button (TL1105) ──── GND
            │
           10kΩ (pullup to 3.3V)
```

### I2C Bus (ATECC608A)
```
SDA (GPIO21) ────┬──── ATECC608A SDA
                 │
                4.7kΩ (pullup to 3.3V)

SCL (GPIO22) ────┬──── ATECC608A SCL
                 │
                4.7kΩ (pullup to 3.3V)
```

### LED Indicators
```
GPIO2 (Status) ──── 1kΩ ──── Green LED ──── GND
GPIO3 (Alert) ───── 1kΩ ──── Red LED ───── GND
```

---

## Assembly Instructions (Prototype)

### Tools Required
- Soldering iron (temperature-controlled)
- Wire strippers
- Multimeter
- Hot glue gun (for strain relief)
- USB-C cable
- Computer with ESP-IDF

### Assembly Steps

1. **Prepare ESP32-S3-DevKitC-1**
   - Solder header pins if not pre-soldered
   - Verify power on (LED should light when USB connected)

2. **Connect ATECC608A (optional for MVP)**
   - SDA: GPIO21
   - SCL: GPIO22
   - VCC: 3.3V
   - GND: GND
   - Add 4.7kΩ pullup resistors on SDA and SCL

3. **Connect External Button (if not using BOOT button)**
   - One side to GPIO0
   - Other side to GND
   - Add 10kΩ pullup resistor (GPIO0 to 3.3V)

4. **Connect Status LED (if not using onboard LED)**
   - Anode to GPIO2 via 1kΩ resistor
   - Cathode to GND

5. **Enclosure Mounting**
   - Drill holes for button and LEDs
   - Secure DevKit with standoffs
   - Add strain relief for USB cable

6. **Testing**
   - Flash test firmware (LED blink)
   - Verify button press detection
   - Test I2C communication with ATECC608A

---

## Production PCB Design Guidelines

### Layout Considerations
- **2-layer PCB**: Sufficient for this design
- **Dimensions**: 45mm × 45mm (fits standard enclosure)
- **Copper**: 1oz (35µm) for power traces
- **Silkscreen**: Component labels, polarity markers
- **Solder mask**: Green (standard)
- **Surface finish**: ENIG (gold plating) for reliability

### Design Rules
- **Trace width**: 0.3mm minimum, 0.5mm for power
- **Trace spacing**: 0.2mm minimum
- **Via size**: 0.4mm drill, 0.8mm pad
- **Pad size**: 0.3mm larger than drill for THT

### Critical Traces
- **Power (5V, 3.3V)**: 1.0mm width minimum
- **I2C (SDA, SCL)**: Keep short, 0.3mm width, 4.7kΩ pullups
- **USB D+/D-**: Impedance-controlled 90Ω differential pair
- **Antenna**: Follow ESP32-S3 reference design, keep clear

### Manufacturing Files
- Gerber RS-274X format
- Excellon drill file
- Pick-and-place file (CSV)
- BOM (Excel/CSV)
- Assembly drawing (PDF)

---

## Next Steps (Hardware Development)

### Phase 1 (Current - Prototype)
- [x] Select ESP32-S3 DevKit
- [x] Define pin assignments
- [ ] Test ATECC608A integration
- [ ] Finalize enclosure design
- [ ] Build 10 prototype units

### Phase 2 (Custom PCB)
- [ ] Schematic capture (KiCad or Altium)
- [ ] PCB layout
- [ ] Design review
- [ ] Order PCB prototypes (5 units)
- [ ] Assembly and testing

### Phase 3 (Production)
- [ ] Radio certifications (FCC, CE)
- [ ] Safety certifications (UL)
- [ ] Injection mold tooling for enclosure
- [ ] Contract manufacturer selection
- [ ] Pilot production run (100 units)

---

## References

- [ESP32-S3 Datasheet](https://www.espressif.com/sites/default/files/documentation/esp32-s3_datasheet_en.pdf)
- [ESP32-S3-DevKitC-1 Schematic](https://dl.espressif.com/dl/schematics/SCH_ESP32-S3-DevKitC-1_V1.1_20220413.pdf)
- [ATECC608A Datasheet](https://ww1.microchip.com/downloads/en/DeviceDoc/ATECC608A-CryptoAuthentication-Device-Summary-Data-Sheet-DS40001977B.pdf)
- [FCC Part 15 Rules](https://www.fcc.gov/wireless/bureau-divisions/technologies-systems-and-innovation-division/rules-regulations-title-47)
- [CE RED Directive](https://ec.europa.eu/growth/sectors/electrical-and-electronic-engineering-industries-eei/radio-equipment-directive-red_en)

---

**Document Version**: 1.0
**Last Updated**: 2025-11-02
**Owner**: Hardware Engineering Lead
