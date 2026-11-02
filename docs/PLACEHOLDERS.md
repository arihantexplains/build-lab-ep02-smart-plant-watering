# Placeholders: every value you fill in

The repo ships with placeholders only. This is the one list of everything you replace, where it lives, and what it looks like. Real values go into the files and settings named here, never into Git.

| # | Placeholder | Where it lives | What it is | Example format |
|---|---|---|---|---|
| 1 | `WIFI_SSID` | `arduino/CallAPI/secrets.h` | Your home Wi-Fi name (2.4 GHz) | `"YOUR_WIFI_NAME"` |
| 2 | `WIFI_PASSWORD` | `arduino/CallAPI/secrets.h` | Wi-Fi password | `"YOUR_WIFI_PASSWORD"` |
| 3 | `API_HOST` | `arduino/CallAPI/secrets.h` | Web API host name, no `https://`, no slash | `"YOUR-API-NAME.azurewebsites.net"` |
| 4 | `DEVICE_KEY` | `arduino/CallAPI/secrets.h` **and** web API app setting `DEVICE_KEY` | Shared key the board sends in the `x-device-key` header. Same value in both places, 24+ random characters. | `"PASTE_DEVICE_KEY_HERE"` |
| 5 | `ROOT_CA` | `arduino/CallAPI/secrets.h` | Root certificate of the API's HTTPS chain, PEM | `-----BEGIN CERTIFICATE-----` ... (DigiCert Global Root G2 for `*.azurewebsites.net` today; check yours) |
| 6 | `MODE` | Web API app setting | `dataverse` on Azure, `mock` on your machine | `dataverse` |
| 7 | `TENANT_ID` | Web API app setting | Directory (tenant) ID of the web API's app registration | `YOUR_TENANT_ID` (a GUID) |
| 8 | `CLIENT_ID` | Web API app setting | Application (client) ID of that app registration | `YOUR_APP_CLIENT_ID` (a GUID) |
| 9 | `CLIENT_SECRET` | Web API app setting (better: Key Vault reference) | Client secret of that app registration | `YOUR_CLIENT_SECRET` or `@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<name>/)` |
| 10 | `DATAVERSE_URL` | Web API app setting, `dataverse/create-schema.mjs` | Environment URL, no trailing slash | `https://<your-org>.crm.dynamics.com` |
| 11 | `DATAVERSE_READ` | Web API app setting | `table` or `customapi` | `table` |
| 12 | `TABLE_SET` | Web API app setting | Entity set name of Switch Command | `cr_switchcommands` |
| 13 | `COL_SWITCH` | Web API app setting | Logical name of the Switch choice column | `cr_switch` |
| 14 | `COL_STATUS` | Web API app setting | Logical name of the Status yes/no column | `cr_status` |
| 15 | `CUSTOM_API` | Web API app setting | Name of the switch states Custom API | `cr_GetSwitchStates` |
| 16 | Publisher prefix | Maker portal publisher, `dataverse/schema.json`, `plugins/SmartWatering.Plugins/Schema.cs` | Prefix of every custom name | `cr` |
| 17 | Table logical names | `Schema.cs`, `schema.json` | Switch Command, Send WhatsApp Message | `cr_switchcommand`, `cr_sendwhatsappmessage` |
| 18 | Column logical names | `Schema.cs`, `schema.json` | See [dataverse/README.md](../dataverse/README.md) | `cr_switch`, `cr_status`, `cr_messagetext`, `cr_receiver`, `cr_receivernumber`, `cr_send`, `cr_deliverystatus`, `cr_result`, `cr_senton` |
| 19 | Switch choice values | Choice column `cr_switch` | Must be exactly these | Motor `100000000`, Ground `100000001`, First `100000002`, Second `100000003` |
| 20 | `cr_WhatsAppAdminNumbers` | Dataverse environment variable (current value) | Admin mobile numbers, comma separated, country code first, digits only | `91XXXXXXXXXX,91XXXXXXXXXX` |
| 21 | `cr_WhatsAppPhoneNumberId` | Dataverse environment variable | WhatsApp phone number ID from Meta > your app > WhatsApp > API Setup. Not the phone number. | `PASTE_PHONE_NUMBER_ID` (digits) |
| 22 | `cr_WhatsAppAccessToken` | Dataverse environment variable, **Secret** type backed by Azure Key Vault | Meta access token. Use a system user token; the temporary token from API Setup expires in 24 hours. | Key Vault secret reference `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/<vault>/secrets/<name>` |
| 23 | `cr_WhatsAppApiVersion` | Dataverse environment variable | Graph API version | `v21.0` (use the current one) |
| 24 | `cr_WhatsAppTemplateName` | Dataverse environment variable | Approved template with one `{{1}}` body placeholder. Empty = plain text. | `watering_alert` |
| 25 | `cr_WhatsAppTemplateLanguage` | Dataverse environment variable | Template language code | `en` |
| 26 | Receiver number (optional) | A Send WhatsApp Message row | One recipient instead of all admins | `91XXXXXXXXXX` |
| 27 | Script settings | Shell environment when running `create-schema.mjs` | A separate setup app registration with System Customizer | `TENANT_ID`, `CLIENT_ID`, `CLIENT_SECRET`, `DATAVERSE_URL`, `SOLUTION`, `WA_ADMIN_NUMBERS`, `WA_PHONE_NUMBER_ID`, `WA_TEMPLATE_NAME`, `WA_TOKEN_KEYVAULT_REF` |
| 28 | Time zone | Scheduled flow trigger | Your local time zone | `(UTC+05:30) Chennai, Kolkata, Mumbai, New Delhi` |
| 29 | `key.snk` | `plugins/SmartWatering.Plugins/key.snk` (git-ignored) | Strong-name key for the plugin assembly | made with `sn -k key.snk` |
| 30 | App Service name | Azure | Your web app name | `YOUR-API-NAME` |

## Quick check before you commit

```bash
git status                      # secrets.h, .env and key.snk must NOT be listed
git grep -n "PASTE_\|YOUR_"     # the repo copies should still show placeholders
```
