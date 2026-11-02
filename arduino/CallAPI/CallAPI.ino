/*
  CallAPI.ino  -  Smart plant watering, ESP32 + 4-channel relay board (v2, 2026)

  Modernised from the original CallAPI.ino (2023) that has watered our plants since then.

  What changed from v1, and why
  - One HTTPS call per poll instead of three GETs plus notification calls per loop.
    The web API answers with four true/false values: motor, groundFloor, firstFloor, secondFloor.
  - Notifications moved to the cloud (a Dataverse plugin creates the WhatsApp message row),
    so the board only does one job: drive the relays.
  - Secrets in secrets.h (never committed), every setting in config.h.
  - HTTPS with the certificate checked and a device key, timeouts on every call.
  - Wi-Fi reconnect without blocking the loop forever.
  - Safety on the board: relays OFF at boot, motor only with a tap open (2 s valve lead),
    10 minute hard limit, everything OFF after 10 failed polls in a row.
  - Relay pins moved off GPIO 12 and 14 (see config.h).

  API
  GET https://YOUR-API-NAME.azurewebsites.net/api/switches     header x-device-key: <DEVICE_KEY>
  200 { "motor": true, "groundFloor": true, "firstFloor": false, "secondFloor": false }
  true = relay ON, false = relay OFF. A missing key counts as false.

  Libraries: ESP32 Arduino core 3.x, ArduinoJson 7.x.  Serial Monitor: 115200 baud.
*/

#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include "secrets.h"    // Wi-Fi, API host, device key, root certificate (never committed)

#include "config.h"     // pins, timings, API path

const int SWITCH_COUNT = 4;
const int MOTOR = 0;   // position of the motor in the table below

struct Switch {
  const char* key;         // JSON key in the API answer
  const char* label;       // name in the Serial Monitor
  uint8_t  pin;            // GPIO that drives the relay input
  bool     want;           // what the API asked for
  bool     isOn;           // what the relay is doing
  bool     latched;        // auto-off happened: ignore "true" until the API says false
  uint32_t onSinceMs;
};

Switch sw[SWITCH_COUNT] = {
  {"motor",       "Motor",                 PIN_MOTOR, false, false, false, 0},
  {"groundFloor", "Ground floor solenoid", PIN_GROUND_FLOOR, false, false, false, 0},
  {"firstFloor",  "First floor solenoid",  PIN_FIRST_FLOOR, false, false, false, 0},
  {"secondFloor", "Second floor solenoid", PIN_SECOND_FLOOR, false, false, false, 0},
};

WiFiClientSecure secureClient;
uint32_t lastPollMs = 0, motorStoppedMs = 0, lastWifiTryMs = 0;
int failedPolls = 0;
bool failSafeActive = false;

// ---------------- Relays ----------------

void setRelay(int i, bool on) {
  if (sw[i].isOn == on) return;
  digitalWrite(sw[i].pin, (on != RELAY_ACTIVE_LOW) ? HIGH : LOW);
  sw[i].isOn = on;
  sw[i].onSinceMs = millis();
  if (i == MOTOR && !on) motorStoppedMs = millis();
  Serial.printf("[RELAY] %s -> %s\n", sw[i].label, on ? "ON" : "OFF");
}

int openValves() {
  int n = 0;
  for (int i = 1; i < SWITCH_COUNT; i++) if (sw[i].isOn) n++;
  return n;
}

uint32_t valveOpenForMs() {
  uint32_t longest = 0;
  for (int i = 1; i < SWITCH_COUNT; i++)
    if (sw[i].isOn) longest = max(longest, (uint32_t)(millis() - sw[i].onSinceMs));
  return longest;
}

void allOff() {
  for (int i = 0; i < SWITCH_COUNT; i++) sw[i].want = false;
  setRelay(MOTOR, false);   // valves follow 2 s later in applyWanted()
}

// ---------------- Decide what the relays do ----------------

void applyWanted() {
  uint32_t now = millis();

  // Valves: open first (so a hand-over from one floor to the next never leaves the motor
  // with every tap shut), then close the ones the app turned off.
  for (int i = 1; i < SWITCH_COUNT; i++)
    if (sw[i].want && !sw[i].latched) setRelay(i, true);
  for (int i = 1; i < SWITCH_COUNT; i++) {
    if (!sw[i].want && sw[i].isOn) {
      bool lastValve = (openValves() == 1);
      if (lastValve && sw[MOTOR].isOn) setRelay(MOTOR, false);
      if (!lastValve || now - motorStoppedMs >= VALVE_CLOSE_MS) setRelay(i, false);
    }
  }

  // Motor: only with an open valve, and only after the valve has had time to open.
  bool motorAllowed = openValves() > 0 && valveOpenForMs() >= VALVE_LEAD_MS;
  if (sw[MOTOR].want && !sw[MOTOR].latched && motorAllowed) setRelay(MOTOR, true);
  if (!sw[MOTOR].want) setRelay(MOTOR, false);
  if (sw[MOTOR].isOn && openValves() == 0) {
    Serial.println("[SAFETY] Motor stopped, no solenoid is open");
    setRelay(MOTOR, false);
  }
}

void enforceTimers() {
  for (int i = 0; i < SWITCH_COUNT; i++) {
    if (sw[i].isOn && millis() - sw[i].onSinceMs >= HARD_MAX_MINUTES * 60000UL) {
      Serial.printf("[SAFETY] %s auto-off after %lu min\n", sw[i].label, (unsigned long)HARD_MAX_MINUTES);
      sw[i].latched = true;
      sw[i].want = false;
    }
  }
}

// ---------------- Cloud ----------------

bool pollSwitches() {
  if (WiFi.status() != WL_CONNECTED) return false;

  HTTPClient http;
  http.setTimeout(HTTP_TIMEOUT_MS);
  if (!http.begin(secureClient, String("https://") + API_HOST + API_PATH)) return false;
  http.addHeader("x-device-key", DEVICE_KEY);
  int code = http.GET();
  if (code != HTTP_CODE_OK) {
    Serial.printf("[POLL] failed, %s\n", code > 0 ? String(code).c_str() : http.errorToString(code).c_str());
    http.end();
    return false;
  }

  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, http.getString());
  http.end();
  if (err) {
    Serial.printf("[POLL] bad JSON, %s\n", err.c_str());
    return false;   // bad data never switches anything ON
  }

  for (int i = 0; i < SWITCH_COUNT; i++) {
    bool want = doc[sw[i].key] | false;            // missing key = OFF
    if (want != sw[i].want) Serial.printf("[POLL] %s wants %s\n", sw[i].label, want ? "ON" : "OFF");
    if (!want && sw[i].latched) {
      sw[i].latched = false;                        // app said OFF: the switch may be used again
      Serial.printf("[SAFETY] %s ready again\n", sw[i].label);
    }
    sw[i].want = want;
  }
  return true;
}

// ---------------- Wi-Fi ----------------

void connectWifi() {
  Serial.print("[WIFI] connecting");
  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  uint32_t start = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - start < 20000) {
    delay(500);
    Serial.print(".");
  }
  Serial.println();
  if (WiFi.status() == WL_CONNECTED)
    Serial.printf("[WIFI] connected, IP %s, signal %d dBm\n", WiFi.localIP().toString().c_str(), WiFi.RSSI());
  else
    Serial.println("[WIFI] not connected yet, will keep trying");
}

void keepWifiAlive() {
  if (WiFi.status() != WL_CONNECTED && millis() - lastWifiTryMs > WIFI_RETRY_MS) {
    lastWifiTryMs = millis();
    Serial.println("[WIFI] reconnecting");
    WiFi.reconnect();
  }
}

// ---------------- Main ----------------

void setup() {
  // Relays OFF before anything else, so nothing clicks on during boot.
  for (int i = 0; i < SWITCH_COUNT; i++) {
    digitalWrite(sw[i].pin, RELAY_ACTIVE_LOW ? HIGH : LOW);
    pinMode(sw[i].pin, OUTPUT);
  }
  Serial.begin(115200);
  delay(300);
  Serial.println();
  Serial.println("[BOOT] CallAPI v2, all relays OFF");

  secureClient.setCACert(ROOT_CA);
  // secureClient.setInsecure();   // TEST ONLY: skips the certificate check. Never ship this.

  connectWifi();
}

void loop() {
  keepWifiAlive();

  if (millis() - lastPollMs >= POLL_EVERY_MS) {
    lastPollMs = millis();
    if (pollSwitches()) {
      if (failSafeActive) Serial.println("[SAFETY] API reachable again, following the app");
      failedPolls = 0;
      failSafeActive = false;
    } else if (++failedPolls >= FAIL_LIMIT && !failSafeActive) {
      Serial.printf("[SAFETY] %d polls failed, everything OFF\n", failedPolls);
      failSafeActive = true;
    }
  }

  if (failSafeActive) allOff();
  enforceTimers();
  applyWanted();
  delay(20);
}
