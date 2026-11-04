# ESP32 sketch: CallAPI

One ESP32 on home Wi-Fi drives a 4-channel relay board: the Motor (main pump) and one solenoid each for the ground, first and second floor drip lines. Every 1.5 seconds it asks the web API for four booleans and sets the relays (true = ON), with its own safety rules on top. The board never talks to Dataverse and holds no Dataverse credentials.

## Files

| File | What it is |
|---|---|
| `CallAPI/CallAPI.ino` | The sketch. |
| `CallAPI/config.h` | Pins, timings and the API path. No secrets. |
| `CallAPI/secrets.example.h` | Template for Wi-Fi, API host, device key and root certificate. |
| `CallAPI/secrets.h` | Your copy of the template. Git-ignored. Never commit it or show it on screen. |

## Wiring (from config.h)

| ESP32 pin | Relay input | Device |
|---|---|---|
| GPIO 26 | IN1 | Motor, the main pump (pulls water from the tank at full speed) |
| GPIO 27 | IN2 | Ground floor solenoid |
| GPIO 25 | IN3 | First floor solenoid |
| GPIO 33 | IN4 | Second floor solenoid |
| GND | GND | Common ground for the relay inputs |
| 5V / VIN | VCC | Relay board logic supply (check your board) |

These pins are plain outputs with no boot role. Avoid GPIO 12 and 14 for relays: GPIO 12 is a boot strapping pin and GPIO 14 pulses at boot, so a relay could stop the board from booting or click on for a moment.

Most relay boards switch ON when the input is LOW. If yours switches ON with HIGH, set `RELAY_ACTIVE_LOW` to `false` in `config.h`.

The mains side (motor, contactor, RCD) is in [../docs/wiring.md](../docs/wiring.md). Read it before you connect anything to 230 V.

## Set up

1. Install the Arduino IDE 2, the **esp32** board package by Espressif (3.x) and the **ArduinoJson** library (7.x).
2. Copy `CallAPI/secrets.example.h` to `CallAPI/secrets.h`.
3. Fill in `WIFI_SSID` and `WIFI_PASSWORD` (a 2.4 GHz network; the ESP32 has no 5 GHz), `API_HOST` (for example `YOUR-API-NAME.azurewebsites.net`, no `https://`, no slash) and `DEVICE_KEY` (the same value as the web API's `DEVICE_KEY` setting).
4. Paste the root certificate of your API's HTTPS chain into `ROOT_CA`. For `*.azurewebsites.net` this is currently DigiCert Global Root G2. Check it in your browser: padlock > certificate > the top of the chain, export it as PEM (Base64) and paste the whole block.
5. Board: **ESP32 Dev Module** (or your board). Upload, then open the Serial Monitor at 115200 baud.

Command line check, the same one used for this repo:

```bash
arduino-cli compile --fqbn esp32:esp32:esp32 --warnings all arduino/CallAPI
```

It needs a `secrets.h` to exist (a copy of the example file is enough to compile). Result for this release: 0 errors, 0 warnings, about 78% of program storage on an ESP32 Dev Module.

`secureClient.setInsecure()` is in the sketch as a commented line for a quick bench test only. It turns off the certificate check, so never leave it in a real install.

## What the API must answer

`GET https://<API_HOST>/api/switches` with header `x-device-key` returns:

```json
{ "motor": true, "groundFloor": true, "firstFloor": false, "secondFloor": false }
```

A missing key counts as false. Any error, timeout or bad JSON counts as a failed poll and never switches anything on.

## Safety rules on the board

| Rule | Where in the code |
|---|---|
| All relays OFF at boot, before Wi-Fi starts. | `setup()` |
| The motor only runs while at least one solenoid is open, so the pump never pushes against closed taps. | `applyWanted()`, message `[SAFETY] Motor stopped, no solenoid is open` |
| Start order: the tap opens first, the motor starts once a tap has been open 2 seconds (`VALVE_LEAD_MS`). | `applyWanted()` |
| Stop order: the motor stops first, the last tap closes about 2 seconds later (`VALVE_CLOSE_MS`). | `applyWanted()` |
| Nothing stays ON longer than 10 minutes (`HARD_MAX_MINUTES`). After that the switch stays off until the API says false once, so a forgotten "true" cannot restart it. | `enforceTimers()` |
| 10 failed polls in a row (`FAIL_LIMIT`, about 15 seconds): everything OFF. It follows the app again after the next good poll. | `loop()` |
| Every HTTPS call gives up after 3 seconds (`HTTP_TIMEOUT_MS`). | `pollSwitches()` |
| Wi-Fi reconnect every 10 seconds without blocking the loop. | `keepWifiAlive()` |

## Serial Monitor (sample)

This is a **sample** of what the Serial Monitor shows, with IDE timestamps on: one manual run from the app, then an API outage. It is written to show the messages, not a live capture.

```text
09:40:58.102 -> [BOOT] CallAPI, all relays OFF
09:40:58.405 -> [WIFI] connecting......
09:41:01.511 -> [WIFI] connected, IP 192.168.1.42, signal -58 dBm
09:41:30.874 -> [POLL] Motor wants ON
09:41:30.876 -> [POLL] Ground floor solenoid wants ON
09:41:30.879 -> [RELAY] Ground floor solenoid -> ON
09:41:32.881 -> [RELAY] Motor -> ON
09:43:31.402 -> [POLL] Motor wants OFF
09:43:31.404 -> [POLL] Ground floor solenoid wants OFF
09:43:31.406 -> [RELAY] Motor -> OFF
09:43:33.410 -> [RELAY] Ground floor solenoid -> OFF
09:51:02.017 -> [POLL] failed, connection refused
09:51:03.536 -> [POLL] failed, connection refused
09:51:05.055 -> [POLL] failed, connection refused
09:51:06.574 -> [POLL] failed, connection refused
09:51:08.093 -> [POLL] failed, connection refused
09:51:09.612 -> [POLL] failed, connection refused
09:51:11.131 -> [POLL] failed, connection refused
09:51:12.650 -> [POLL] failed, connection refused
09:51:14.169 -> [POLL] failed, connection refused
09:51:15.688 -> [POLL] failed, connection refused
09:51:15.690 -> [SAFETY] 10 polls failed, everything OFF
09:52:40.220 -> [SAFETY] API reachable again, following the app
```

## Messages and what they mean

| Serial line | Meaning |
|---|---|
| `[WIFI] not connected yet, will keep trying` | Wrong SSID or password, a 5 GHz network, or weak signal. |
| `[POLL] failed, 401` | `DEVICE_KEY` on the board does not match the API's `DEVICE_KEY`. |
| `[POLL] failed, 502` | The API is up but cannot read Dataverse. See the web API log. |
| `[POLL] failed, connection refused` | No HTTPS connection. Wrong `API_HOST`, the API is down, or the certificate check failed (wrong `ROOT_CA`). |
| `[POLL] bad JSON, ...` | The API answered with something that is not JSON. Check `API_PATH`. |
| `[SAFETY] Motor stopped, no solenoid is open` | The app asked for the motor with every tap closed. Open a tap first. |
| `[SAFETY] <switch> auto-off after 10 min` | The 10 minute limit. Turn it OFF in the app, then ON again. |
