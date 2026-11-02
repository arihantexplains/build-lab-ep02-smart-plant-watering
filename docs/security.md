# Security: what protects what, and the honest limits

This is a home project that switches a real pump and sends real messages, so it is worth being clear about what the code does and what it does not.

## Guardrails and where they live

| Layer | Guardrail | Where |
|---|---|---|
| Device to API | HTTPS with the server certificate checked against a pinned root (`ROOT_CA`), not `setInsecure()` | `arduino/CallAPI/CallAPI.ino`, `setup()` |
| Device to API | Device key in the `x-device-key` header, compared in constant time | `webapi/server.mjs`, `keyOk()` |
| API start-up | Dataverse mode refuses to start with the sample key or a key under 24 characters | `webapi/server.mjs` |
| API to Dataverse | Client credentials of an app registration whose application user has a **read-only** role on Switch Command | App settings, Dataverse role |
| Secrets on the device | Wi-Fi, key and certificate in `secrets.h`, git-ignored | `arduino/CallAPI/secrets.h` |
| Secrets in the cloud | App Service settings (Key Vault reference recommended), Dataverse environment variables (Secret type in Key Vault for the token) | Azure, Dataverse |
| Bad data | A missing key reads as OFF; bad JSON, errors and timeouts never switch anything ON | `pollSwitches()` |
| Lost connection | 10 failed polls in a row: everything OFF | `loop()` |
| Forgotten ON | 10 minute hard limit per switch, latched until the API says OFF | `enforceTimers()` |
| Dry running pump | Motor only with an open tap, tap first, motor last | `applyWanted()` |
| Who can send WhatsApp messages | Alert rows are created by the plugin as SYSTEM, so app users need no rights on the message table | `SwitchCommandNotifyPlugin` |
| Repeat sends | Send set back to No, flow fires only on a change of Send, rows already Sent are skipped, flow retry off | Send plugin, send flow |
| Numbers | Validated (digits, country code, length) and masked in the Result column and traces | `PhoneNumbers.cs` |
| Meta call | HTTPS only, 15 s timeout, token never traced, URL parts validated | `WhatsAppClient.cs`, `WhatsAppPayload.cs` |

## Dataverse roles

| Who | Role |
|---|---|
| Web API application user | Read on Switch Command (Organization). Nothing else. |
| People who use the canvas app | Create and Read on Switch Command. **No** access to Send WhatsApp Message. |
| Send flow connection user | Read and Write on Send WhatsApp Message, Read on environment variable definitions and values. |
| Admins who may send messages by hand | Create on Send WhatsApp Message. |
| Setup script app (optional) | System Customizer, only while you run `create-schema.mjs`. Delete its secret afterwards. |

## Known limits (read these)

- **The device key is a shared secret, not a device identity.** Anyone who gets it can read the four switch states. It cannot switch anything (the endpoint is read-only), but treat it like a password and change it if the board is lost. There is no rate limit on the API; App Service access restrictions or Azure Front Door can add one.
- **The key and Wi-Fi password sit in the ESP32's flash.** Someone with the board in hand can read them. The ESP32 supports flash encryption and secure boot; this sample does not turn them on.
- **The root certificate can change.** If Azure moves `*.azurewebsites.net` to a different root, the board stops connecting (`[POLL] failed, connection refused`) and, after 10 polls, switches everything OFF. That is the safe direction, but you will need to update `ROOT_CA`. A custom domain with your own certificate gives you more control.
- **Mock mode has no persistence and is for the bench only.** Its POST route needs the device key, but never expose it on the internet.
- **Anyone who can create a Send WhatsApp Message row can message any number** from your WhatsApp business number, by filling in Receiver number. Keep Create on that table for admins only (see the roles above). The plugin does not check the number against an allow-list.
- **A Text environment variable for the token is readable** by anyone who can read environment variables in the environment. Use the Secret type with Key Vault outside a quick demo. The plugin reads Secret variables with `RetrieveEnvironmentVariableSecretValue`; that path is written from the documentation and was not run against a live environment for this release.
- **"Sent" means Meta accepted the request**, not that the phone received it. Delivery failures such as error `131047` (outside the 24 hour window without a template) can arrive later through a webhook, which this sample does not handle.
- **Anyone with Create on Switch Command can water the garden.** That is the point of the app, so share it only with the right people.
- **The safety rules are software.** Relays can weld shut and software can have bugs. The mains side needs its own protection: RCD, MCB, a contactor rated for the motor, and an electrician. See [wiring.md](wiring.md).

## Reporting a problem

See [SECURITY.md](../SECURITY.md).
