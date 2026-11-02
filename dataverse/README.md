# Dataverse: tables, columns, environment variables, Custom APIs

Everything the system needs in Dataverse is described once in [`schema.json`](schema.json). You can build it by hand in the maker portal (steps below) or with [`create-schema.mjs`](create-schema.mjs).

> **The script is untested against a live environment.** It was written from the Dataverse Web API metadata documentation and checked only with `--dry-run`, which prints every request without sending it. Run it in a **demo** environment, read its output, and fall back to the manual steps if anything fails. The manual steps are the reference.

The sample uses the publisher prefix **`cr`**. If yours is different, change the prefix everywhere in `schema.json`, in `plugins/SmartWatering.Plugins/Schema.cs`, and in the web API settings `TABLE_SET`, `COL_SWITCH`, `COL_STATUS`.

## What gets created

### Table: Switch Command (`cr_switchcommand`, entity set `cr_switchcommands`)

One row per ON/OFF order. Nothing is ever updated: every toggle is a new row, and the newest row per switch wins.

| Column | Logical name | Type | Values |
|---|---|---|---|
| Name | `cr_name` | Text (100), primary | For example "Motor ON". |
| Switch | `cr_switch` | Choice, required | Motor = `100000000`, Ground floor solenoid = `100000001`, First floor solenoid = `100000002`, Second floor solenoid = `100000003` |
| Status | `cr_status` | Yes/No | ON = Yes (true), OFF = No (false). Default OFF. |
| Source | `cr_source` | Text (50), optional | App or Schedule, for the history. |

### Table: Send WhatsApp Message (`cr_sendwhatsappmessage`, entity set `cr_sendwhatsappmessages`)

One row per message. A new row with Send = Yes starts the send flow.

| Column | Logical name | Type | Notes |
|---|---|---|---|
| Name | `cr_name` | Text (200), primary | Short title, the alert plugin uses the message text. |
| Message text | `cr_messagetext` | Multiline text (4000), required | The text to send. |
| From | `cr_from` | Text (100) | Who or what created it, for example "Smart watering". |
| Receiver | `cr_receiver` | Text (100) | Empty or "All admins" = every admin number. Otherwise the receiver's name. |
| Receiver number | `cr_receivernumber` | Text (20) | Country code first, for example `91XXXXXXXXXX`. When filled, only this number gets the message. |
| Send | `cr_send` | Yes/No | Yes starts the flow. The plugin sets it back to No. |
| Delivery status | `cr_deliverystatus` | Choice | Pending = `100000000`, Sent = `100000001`, Partly sent = `100000002`, Failed = `100000003`. Default Pending. |
| Result | `cr_result` | Multiline text (4000) | One line per recipient, numbers masked. |
| Sent on | `cr_senton` | Date and time | When the plugin last tried. |

### Environment variables

| Schema name | Type | Value (placeholder) |
|---|---|---|
| `cr_WhatsAppAdminNumbers` | Text | `91XXXXXXXXXX,91XXXXXXXXXX` (comma separated, country code first) |
| `cr_WhatsAppPhoneNumberId` | Text | `PASTE_PHONE_NUMBER_ID` (digits, from Meta > WhatsApp > API Setup) |
| `cr_WhatsAppAccessToken` | **Secret** (Azure Key Vault), or Text for a quick demo | Key Vault secret holding the Meta access token |
| `cr_WhatsAppApiVersion` | Text | Default `v21.0`. Use the current Graph API version from Meta's changelog. |
| `cr_WhatsAppTemplateName` | Text | `watering_alert` (your approved template), or empty for plain text inside the 24 hour window |
| `cr_WhatsAppTemplateLanguage` | Text | Default `en` |

The plugin reads them from the `environmentvariabledefinition` and `environmentvariablevalue` tables (current value first, default value second). A **Secret** variable keeps its value in Azure Key Vault; the plugin reads it with the `RetrieveEnvironmentVariableSecretValue` message. That is the safer choice for the token: with a Text variable, anyone who can read environment variables in the environment can read the token.

### Custom APIs

| Unique name | Kind | Inputs | Outputs | Plugin type |
|---|---|---|---|---|
| `cr_GetSwitchStates` | Function, unbound (Binding type Global) | none | `Motor`, `GroundFloor`, `FirstFloor`, `SecondFloor` (Boolean) | `SmartWatering.Plugins.GetSwitchStatesPlugin` |
| `cr_SendWhatsAppMessage` | Action, unbound (Binding type Global) | `MessageId` (Guid, required) | `SentCount` (Integer), `FailedCount` (Integer), `Result` (String) | `SmartWatering.Plugins.SendWhatsAppMessagePlugin` |

Both: Allowed custom processing step type None, Is private No.

## Option A: the script

1. In the maker portal of your **demo** environment, create a publisher with prefix `cr` (or your own) and an unmanaged solution, unique name `SmartWatering`.
2. Create an app registration for the script, add it as an application user in the demo environment with the **System Customizer** role. This is a different identity from the web API's read-only one; delete its secret when you are done.
3. Run a dry run, then the real run:

   ```bash
   cd dataverse
   node create-schema.mjs --dry-run

   export TENANT_ID=YOUR_TENANT_ID CLIENT_ID=YOUR_SETUP_APP_CLIENT_ID CLIENT_SECRET=YOUR_SETUP_APP_SECRET
   export DATAVERSE_URL=https://<your-org>.crm.dynamics.com
   # optional values for the environment variables
   export WA_ADMIN_NUMBERS=91XXXXXXXXXX,91XXXXXXXXXX WA_PHONE_NUMBER_ID=PASTE_PHONE_NUMBER_ID WA_TEMPLATE_NAME=watering_alert
   # optional: makes the token variable a Secret type pointing at Key Vault
   export WA_TOKEN_KEYVAULT_REF=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/<vault>/secrets/<name>
   node create-schema.mjs
   ```

   It skips anything that already exists, so you can run it again. Every line starts with `ok`, `FAIL` or `note`.

4. If you ran it before registering the plugin assembly, the Custom APIs have no plugin type yet. Set it in the maker portal (open the Custom API row) or the Plugin Registration Tool.

## Option B: by hand in the maker portal

1. **Solution.** make.powerapps.com > your demo environment > Solutions > New solution `Smart Watering`, publisher with prefix `cr`. Do everything below inside it.
2. **Switch Command table.** New > Table > Table. Display name `Switch Command`, primary column `Name`. Add the columns from the table above. For **Switch**, choose Data type Choice > Choice, "Sync with global choice" No, and add the four options. Check that the values are `100000000` to `100000003` in that order (Advanced options > Value); the web API, the plugins and the ESP32 mapping depend on them. For **Status**, choose Yes/No and rename the labels to ON and OFF.
3. **Send WhatsApp Message table.** Same way, with the columns above. Delivery status values `100000000` to `100000003`, default Pending.
4. **Environment variables.** New > More > Environment variable, one per row of the table above. For the token, Data type **Secret**, Secret Store Azure Key Vault, and follow "Use Azure Key Vault secrets" on Microsoft Learn to give Dataverse access to the vault.
5. **Custom APIs.** New > More > Other > Custom API (or open the Custom API table). Fill in Unique name, Name, Display name, Binding type Global, Is function (Yes for `cr_GetSwitchStates`, No for `cr_SendWhatsAppMessage`), Allowed custom processing step type None, and the Plugin type after you register the assembly. Then add the request parameters and response properties from the table above as **Custom API Request Parameter** and **Custom API Response Property** rows.
6. **Security roles.** See [../docs/security.md](../docs/security.md#dataverse-roles): a read-only role for the web API's application user, and who may write to which table.
7. **Publish all customizations.**

## Check it

- Add a Switch Command row by hand (Switch = Motor, Status = ON). After a few seconds an asynchronous job should create a Send WhatsApp Message row "Motor started" (once the plugin step is registered).
- Call the function in a browser while signed in: `https://<your-org>.crm.dynamics.com/api/data/v9.2/cr_GetSwitchStates()`. It should answer with `Motor`, `GroundFloor`, `FirstFloor`, `SecondFloor`.
