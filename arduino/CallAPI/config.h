// config.h - everything you may want to tune, in one place. No secrets here (those live in secrets.h).
#pragma once

// ---------------- Relay pins ----------------
// The 2023 version used GPIO 13, 12 and 14. Two of those are risky for relays:
//   GPIO 12 is a boot strapping pin (a relay board pulling it HIGH can stop the ESP32 from booting),
//   GPIO 14 puts out a short PWM signal at boot (a relay can click on for a moment).
// These four pins are plain outputs with no boot role.
#define PIN_MOTOR         26   // relay 1: Motor, the main pump
#define PIN_GROUND_FLOOR  27   // relay 2: Ground floor solenoid
#define PIN_FIRST_FLOOR   25   // relay 3: First floor solenoid
#define PIN_SECOND_FLOOR  33   // relay 4: Second floor solenoid

// Most relay boards switch ON when the input pin is LOW. Set to false if yours is the other way round.
#define RELAY_ACTIVE_LOW  true

// ---------------- API ----------------
#define API_PATH          "/api/switches"   // GET, answers {"motor":..,"groundFloor":..,"firstFloor":..,"secondFloor":..}
#define HTTP_TIMEOUT_MS   3000              // give up on one call after 3 s

// ---------------- Timing and safety ----------------
#define POLL_EVERY_MS     1500   // ask the API every 1.5 seconds
#define FAIL_LIMIT        10     // 10 failed polls in a row (about 15 s) -> everything OFF
#define HARD_MAX_MINUTES  10     // no switch stays ON longer than this, whatever the app says
#define VALVE_LEAD_MS     2000   // a tap is open this long before the motor may start
#define VALVE_CLOSE_MS    2000   // the last tap closes this long after the motor stops
#define WIFI_RETRY_MS     10000  // try to reconnect Wi-Fi every 10 s while it is down
