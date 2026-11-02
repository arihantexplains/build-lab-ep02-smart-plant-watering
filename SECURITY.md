# Security

This is a teaching sample from a YouTube episode, not a supported product. It switches a real pump and sends real messages, so please read this before you run it anywhere.

## Use a demo environment first

- Build and test the Power Platform parts in a demo or developer environment.
- Test the ESP32 on the bench with the relays only, no mains connected.
- Mains wiring (motor, contactor, RCD, MCB, any 230 V solenoids) is a job for a qualified electrician. See [docs/wiring.md](docs/wiring.md).
- When you finish a demo, rotate or delete the client secrets and the Meta access token.

## Keep your settings out of Git

The repo contains placeholders only. Your Wi-Fi password, device key, tenant ID, client ID, client secret, environment URL, WhatsApp phone number ID, access token and admin numbers belong in `arduino/CallAPI/secrets.h` (git-ignored), App Service settings, Azure Key Vault and Dataverse environment variables, never in code, commits or issues. The `.gitignore` blocks `secrets.h`, `.env`, signing keys, certificates and solution exports, but check `git status` before every commit anyway.

If you open an issue, remove keys, IDs, URLs, phone numbers and names from logs and screenshots first.

## What the sample does to stay safe

The guardrails are described in [docs/security.md](docs/security.md): HTTPS with a pinned root certificate and a device key between the board and the API, a read-only service account for Dataverse, safety rules on the board (all OFF at boot, motor only with an open tap, a 10 minute limit, everything OFF when the API is lost), alert rows created by the plugin as SYSTEM, validated and masked phone numbers, and the Meta token in a Secret environment variable. The same page lists the known limits.

## Reporting a problem

If you find a security problem in the code, please don't open a public issue with exploit details. Use GitHub's **Report a vulnerability** button on the Security tab of this repository (private vulnerability reporting), or open an issue that just says you found something and would like a private way to share it.

There is no bug bounty and no guaranteed response time, but I read every report and will fix real problems in the sample.
