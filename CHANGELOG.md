# Changelog

All notable changes to this repo. Dates are in the format YYYY-MM-DD.

## [1.0.0] - 2026-11-02

First public release, alongside Architect's Build Lab, Episode 4.

### Added

- ESP32 sketch `CallAPI`: one HTTPS call per poll with a pinned root certificate and a device key, four relays (Motor, Ground, First and Second floor solenoid), settings in `config.h`, secrets in a git-ignored `secrets.h`.
- Node web API (no packages) that answers with four booleans: mock mode for the bench, Dataverse mode with a read-only service account, and an optional read through the `cr_GetSwitchStates` Custom API. 15 automated checks.
- C# Dataverse plugins: `SwitchCommandNotifyPlugin` (an ON row creates a WhatsApp message row), `GetSwitchStatesPlugin` (newest row per switch, one query) and `SendWhatsAppMessagePlugin` (Meta WhatsApp Cloud API, every admin number or one receiver). Pure logic in a tested helper layer, 41 unit tests.
- Dataverse schema as JSON and markdown, and `create-schema.mjs` to create it through the Web API (checked with a dry run only so far).
- Step-by-step docs for the scheduled watering flow and the send WhatsApp flow, the canvas app, architecture, setup, placeholders, security and wiring.

### Security

- Safety rules on the board: all relays OFF at boot, motor only with an open tap (tap 2 s before the motor, motor stops 2 s before the last tap), 10 minute hard limit, everything OFF after 10 failed polls.
- Relay pins moved off GPIO 12 and 14.
- The web API refuses to start in Dataverse mode with the sample or a short device key, and the mock routes need the device key too.
- WhatsApp alert rows are created as SYSTEM; numbers are validated and masked; the token is read from a Secret environment variable when available.
