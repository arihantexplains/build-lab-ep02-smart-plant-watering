# Web API: one call, four booleans

The ESP32 never talks to Dataverse. It calls this small API every 1.5 seconds and gets one answer:

```http
GET /api/switches
x-device-key: <DEVICE_KEY>
```

```json
{ "motor": true, "groundFloor": true, "firstFloor": false, "secondFloor": false }
```

`true` means relay ON. Behind it, the API signs in to Dataverse with a service account (an app registration and its application user with a read-only role) and works out the newest row per switch.

Node 18 or newer, no packages, two files:

| File | What it is |
|---|---|
| `server.mjs` | The API. Mock mode for the bench, Dataverse mode for real. |
| `test.mjs` | 15 checks: mock mode, Dataverse mode against a local fake Dataverse and token endpoint, the Custom API option, and the start-up refusal of the sample key. |
| `.env.sample` | Every setting, placeholders only. |

## Run it locally

```bash
cd webapi
npm test                      # or: node test.mjs
MODE=mock DEVICE_KEY=any-long-test-key-123456 npm start
```

In mock mode you play the canvas app or the flow by posting rows (the device key is needed here too):

```bash
curl -X POST localhost:8080/api/mock/rows -H "x-device-key: any-long-test-key-123456" \
     -d '{"switch":"groundFloor","on":true,"source":"App"}'
curl localhost:8080/api/switches -H "x-device-key: any-long-test-key-123456"
```

Never put mock mode on the internet. It is for the bench only.

## Two ways to read Dataverse

| `DATAVERSE_READ` | What the API does | Use it when |
|---|---|---|
| `table` (default) | One query on the Switch Command table: newest 50 rows, newest row per switch wins. | You want the fewest moving parts. |
| `customapi` | Calls the `cr_GetSwitchStates` Custom API. The C# plugin does the same work inside Dataverse and returns `Motor`, `GroundFloor`, `FirstFloor`, `SecondFloor`. | You want the rule in one place inside Dataverse, shared with other callers. |

Both need the same read-only access for the service account. Both answer with exactly the same JSON, so the ESP32 does not care which one you pick.

## App settings

| Setting | Example (placeholder) | Notes |
|---|---|---|
| `MODE` | `dataverse` | `mock` only on your own machine. |
| `DEVICE_KEY` | `PASTE_DEVICE_KEY_HERE` | Long random value, at least 24 characters. Dataverse mode refuses to start with the sample value or a short one. |
| `TENANT_ID` | `YOUR_TENANT_ID` | Directory (tenant) ID of the app registration. |
| `CLIENT_ID` | `YOUR_APP_CLIENT_ID` | Application (client) ID. |
| `CLIENT_SECRET` | `YOUR_CLIENT_SECRET` | Better as a Key Vault reference: `@Microsoft.KeyVault(SecretUri=...)`. |
| `DATAVERSE_URL` | `https://<your-org>.crm.dynamics.com` | No trailing slash. |
| `DATAVERSE_READ` | `table` | Or `customapi`. |
| `CUSTOM_API` | `cr_GetSwitchStates` | Only for `customapi`. |
| `TABLE_SET` | `cr_switchcommands` | Entity set name (plural) of the Switch Command table. |
| `COL_SWITCH` | `cr_switch` | Choice column. |
| `COL_STATUS` | `cr_status` | Yes/No column, Yes = ON. |
| `PORT` | set by App Service | Leave it alone on Azure. |

Make a device key with, for example, `node -e "console.log(require('crypto').randomBytes(24).toString('base64url'))"`.

## Deploy to Azure App Service

1. **Create the web app.** Azure portal > App Services > Create > Web App. Publish: Code, Runtime stack: Node 20 LTS (or newer), Operating system: Linux, the smallest plan is enough (B1, or F1 for a test). Name it, for example `YOUR-API-NAME`, which gives `YOUR-API-NAME.azurewebsites.net`.
2. **HTTPS only.** Settings > Configuration > General settings: HTTPS Only = On, Minimum TLS version 1.2.
3. **App settings.** Settings > Environment variables: add every setting from the table above (not `PORT`). Put `CLIENT_SECRET` in Key Vault and use a Key Vault reference if you can.
4. **Startup command.** Settings > Configuration > Startup Command: `node server.mjs`.
5. **Deploy the code.** From this folder, either zip deploy (`az webapp deploy --resource-group YOUR-RG --name YOUR-API-NAME --src-path webapi.zip --type zip`, where the zip holds `server.mjs` and `package.json`) or connect the repo in Deployment Center.
6. **Test it.** `https://YOUR-API-NAME.azurewebsites.net/health` should answer `{"ok":true,"mode":"dataverse"}`. Then:

   ```bash
   curl https://YOUR-API-NAME.azurewebsites.net/api/switches -H "x-device-key: <your key>"
   ```

   No key gives `401 {"error":"device key"}`. A Dataverse problem gives `502 {"error":"upstream"}` and a line in Log stream such as `token 401` or `dataverse 403`.

## Errors you may see

| Response or log line | Meaning |
|---|---|
| `401 {"error":"device key"}` | Missing or wrong `x-device-key` header. |
| `502 {"error":"upstream"}` | The API could not read Dataverse. Check Log stream. |
| `token 400` or `token 401` in the log | Wrong tenant ID, client ID or secret, or the secret expired. |
| `dataverse 401` or `dataverse 403` | The application user is missing in the environment or has no read access to Switch Command. |
| `dataverse 404` | Wrong `TABLE_SET` or `CUSTOM_API` name, or the Custom API is not created. |
| `DEVICE_KEY is missing or too short (24 characters or more). Refusing to start in dataverse mode.` | Set a real device key in the app settings. |
