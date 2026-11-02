# Setup, bottom-up, in a demo environment

Build one layer at a time and test it before you add the next. If something breaks later, you know which layer to look at.

Use a **demo or developer environment** for everything in Power Platform, and test the hardware on the bench (relays clicking, no mains connected) before an electrician wires the motor.

Keep [PLACEHOLDERS.md](PLACEHOLDERS.md) open: it lists every value you fill in.

## 0. What you need

- A Power Platform environment with Dataverse (a developer environment is free).
- An Azure subscription for App Service (and Key Vault if you use it).
- A Meta developer app with the WhatsApp product, a WhatsApp Business test or real number, and its phone number ID.
- Node.js 18 or newer, any recent .NET SDK, the Power Platform CLI (`pac`) for the Plugin Registration Tool, the Arduino IDE 2.
- An ESP32 dev board, a 4-channel relay module, a USB cable. The motor and solenoids come last.

## 1. Dataverse tables and environment variables

1. Create a publisher (prefix `cr`) and a solution `Smart Watering`.
2. Create the two tables, their columns and the six environment variables, by hand or with the script: [dataverse/README.md](../dataverse/README.md).

**Test:** add a Switch Command row by hand (Motor, ON). It saves, and the Switch column shows four options with values `100000000` to `100000003`.

## 2. The web API, first in mock mode on your machine

```bash
cd webapi
npm test                                   # 15 checks passed
MODE=mock DEVICE_KEY=any-long-test-key-123456 npm start
curl localhost:8080/api/switches -H "x-device-key: any-long-test-key-123456"
```

**Test:** you get `{"motor":false,"groundFloor":false,"firstFloor":false,"secondFloor":false}`, and without the header you get `401`.

## 3. The ESP32 against the mock API (bench, no mains)

1. Wire the ESP32 to the relay board only (see [arduino/README.md](../arduino/README.md)).
2. For this bench test only, point `API_HOST` at your PC and serve over HTTPS, **or** deploy step 4 first and come back. The sketch always uses HTTPS with the certificate check.
3. Post rows to the mock API and watch the relays and the Serial Monitor.

**Test:** `[RELAY] Ground floor solenoid -> ON`, then `[RELAY] Motor -> ON` two seconds later. Stop the API: after about 15 seconds, `[SAFETY] 10 polls failed, everything OFF`.

## 4. The web API on Azure, Dataverse mode

1. **App registration** in Entra ID for the web API: name it, for example, `smart-watering-api`, add a client secret (short expiry for a demo).
2. **Application user** in the demo environment: Power Platform admin center > Environments > your environment > Settings > Users + permissions > Application users > New app user, pick the app registration, and give it a **read-only role**: a new security role with **Read** (Organization) on Switch Command. If calls still fail with 403, compare with the Basic User role for the few core read rights it may need, but never add write rights. The `customapi` mode needs nothing extra, unless you set an Execute Privilege Name on the Custom API.
3. **App Service**: follow [webapi/README.md](../webapi/README.md#deploy-to-azure-app-service) with `MODE=dataverse`, a real `DEVICE_KEY` and the app registration's values.

**Test:** `/health` answers `{"ok":true,"mode":"dataverse"}`. Add a Switch Command row (Ground floor solenoid, ON) in the maker portal and call `/api/switches` with the key: `groundFloor` is `true`.

## 5. The ESP32 against the real API

Put the real `API_HOST`, `DEVICE_KEY` and `ROOT_CA` in `secrets.h` and upload.

**Test:** add rows in the maker portal and watch the relays follow within about 2 seconds.

## 6. The canvas app

Build it from [app/README.md](../app/README.md).

**Test:** tap Ground floor, then Motor. Both relays click (motor 2 seconds after the tap). Tap Motor with all taps off: the board refuses with `[SAFETY] Motor stopped, no solenoid is open`.

## 7. The plugins

1. Build and test: [plugins/README.md](../plugins/README.md). `dotnet build -c Release` gives 0 warnings, `dotnet test` passes.
2. Create `key.snk`, rebuild, and register the assembly with `pac tool prt`.
3. Register the step for `SwitchCommandNotifyPlugin` (Create, `cr_switchcommand`, PostOperation, Asynchronous).
4. Set the plugin types on the two Custom APIs.

**Test:** add a Switch Command row Motor ON. Within a few seconds a Send WhatsApp Message row "Motor started" appears with Send = Yes and Delivery status Pending. Add an OFF row: no message. Call `https://<your-org>.crm.dynamics.com/api/data/v9.2/cr_GetSwitchStates()` in the browser: four booleans that match the table.

Optional: switch the web API to `DATAVERSE_READ=customapi`. The ESP32 keeps working with no change.

## 8. WhatsApp

1. In your Meta app: WhatsApp > API Setup gives the **phone number ID**. For anything past a quick test, create a **system user** in Meta Business settings and generate a token with the `whatsapp_business_messaging` permission; the temporary token on API Setup expires in 24 hours.
2. Put the token in Azure Key Vault and point the `cr_WhatsAppAccessToken` Secret environment variable at it (or, for a short demo only, a Text variable).
3. Fill in `cr_WhatsAppPhoneNumberId` and `cr_WhatsAppAdminNumbers` (`91XXXXXXXXXX,91XXXXXXXXXX`). With a test number, add each admin number as a recipient in API Setup first.
4. Build the send flow: [flows/send-whatsapp-message.md](../flows/send-whatsapp-message.md).

**Test, inside the 24 hour window:** message the business number from your phone, leave `cr_WhatsAppTemplateName` empty, add a Send WhatsApp Message row for your number with Send = Yes. Delivery status turns Sent and the message arrives.

**Then the template:** create a Utility template (for example `watering_alert`, body `Smart watering: {{1}}`), wait for approval, set `cr_WhatsAppTemplateName`. Test again with "All admins". Now alerts arrive even when nobody has written to the business number.

## 9. The schedule

Build it from [flows/scheduled-watering.md](../flows/scheduled-watering.md), test with short delays, then set them back to 2 minutes.

**Test:** one full run on the bench. You see the hand-over in the Serial Monitor (the next tap opens before the previous one closes, the motor never stops in between) and four WhatsApp alerts.

## 10. The real installation

Only now: an electrician wires the motor through a contactor, the RCD and MCB, and the solenoids on their own supply. Read [wiring.md](wiring.md). Then run one schedule with water while you watch, before you leave it alone.

## Clean up after a demo

- Delete or rotate the client secrets (web API and setup script) and the Meta token.
- Remove the application users you no longer need.
- Stop or delete the App Service.
