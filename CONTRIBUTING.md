# Contributing

Thanks for taking the time. This repo is the companion to a YouTube episode, so the main goal is that someone can follow the video and the docs and get the same result. Fixes that make that easier are the most welcome.

## Good contributions

- A setup step that didn't work for you, and what fixed it.
- A board, relay module or Arduino core version that needed a change, and the change.
- A bug in the sketch, the web API or a plugin, with the steps to reproduce it.
- Clearer docs or comments.
- Hardening that keeps the safety rules intact, for example flash encryption on the ESP32 or a recipient allow-list in the send plugin.

Bigger changes, like a new switch type or a different messaging channel, are best discussed in an issue first, so we agree on the shape before you spend the time.

## Before you open a pull request

1. **Build and test it.**
   - Plugins: `cd plugins/SmartWatering.Plugins` then `dotnet build -c Release` (no errors, no new warnings; every public member has an XML doc comment, a missing one shows up as warning CS1591), then `cd ../SmartWatering.Plugins.Tests` and `dotnet test -c Release`.
   - Web API: `cd webapi` then `npm test`.
   - Sketch: `arduino-cli compile --fqbn esp32:esp32:esp32 --warnings all arduino/CallAPI` (with a `secrets.h` copied from the example), no warnings.
2. **Test it safely.** Power Platform parts in a demo environment, hardware on the bench without mains.
3. **Scrub it.** No Wi-Fi details, device keys, tenant IDs, client IDs, secrets, tokens, GUIDs from your environment, phone numbers, email addresses, environment URLs or real names in code, docs, screenshots or commit messages. Use placeholders like `91XXXXXXXXXX` and `<your-org>`.
4. **Keep the safety rules.** Never remove the certificate check, the fail-safe, the 10 minute limit or the tap-before-motor interlock. Never log a key, a token or a full phone number.
5. **Update the docs** that your change touches (including [docs/PLACEHOLDERS.md](docs/PLACEHOLDERS.md) for any new setting) and add a line to `CHANGELOG.md`.

## Writing style for docs

Plain, friendly English, like explaining to a colleague. Short sentences. No em dashes.

## Security issues

Please don't open a public issue for a security problem. See [SECURITY.md](SECURITY.md).

By contributing you agree that your work is released under the [MIT License](LICENSE).
