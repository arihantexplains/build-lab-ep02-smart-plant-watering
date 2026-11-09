# Permissions (least privilege)

Every identity in this build gets the smallest set of rights that still works. The full role table and the known limits are in [security.md](security.md); this page explains each permission and what was left out on purpose.

## Identities and what they may do

| Identity | Permission | Why it needs it |
|---|---|---|
| ESP32 | A device key for one endpoint, `GET /api/switches` | It only reads four true or false values. It holds no Dataverse credentials and cannot change anything. |
| Web API service account (app registration + application user) | Dataverse security role: Read on Switch Command, Organization level | To read the newest row per switch. It does not need Graph permissions or any other table. |
| People using the canvas app | Create and Read on Switch Command | Each toggle adds one row with their name on it. They have no access to Send WhatsApp Message. |
| Scheduled flow connection | Create on Switch Command | It writes rows floor by floor, exactly like the app. |
| Send flow connection | Read and Write on Send WhatsApp Message, Read on environment variables | To start the send plugin and read the result. |
| Alert plugin (`SwitchCommandNotifyPlugin`) | Runs as SYSTEM inside Dataverse | It creates the message row, so app users never need rights on the message table. |
| Meta WhatsApp token | A permanent system-user token with `whatsapp_business_messaging` | To send messages from the business number. It lives in a secure environment variable backed by Key Vault, never in a flow or in code. |
| Setup script app (optional) | System Customizer, only while `create-schema.mjs` runs | To create the tables. Delete its secret afterwards. |

## Left out on purpose

- **No write access for the web API.** It is read-only, so a stolen device key or a bug in the API cannot switch anything on.
- **No Dataverse sign-in on the ESP32.** No client secret on a board that sits on a terrace.
- **No inbound port at home.** The board calls out over HTTPS; nothing on the internet can start a connection to it.
- **No personal sign-in for automation.** The service account is an app registration with its own identity. If its secret leaks, rotate it; your own user is never at risk.
- **No allow-list on message numbers.** The send plugin checks the format of each number but does not compare it with a list. Keep Create on Send WhatsApp Message for admins only (see [security.md](security.md#known-limits-read-these)).
