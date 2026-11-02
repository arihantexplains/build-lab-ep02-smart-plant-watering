# Dataverse plugins (C#)

One plugin assembly, `SmartWatering.Plugins`, with three plugins, plus a test project for the pure logic.

| Plugin | Registered as | What it does |
|---|---|---|
| `SwitchCommandNotifyPlugin` | Step: **Create** of `cr_switchcommand`, **PostOperation (40)**, **Asynchronous** | When the new row says ON, adds a Send WhatsApp Message row: Motor gives "Motor started", a floor gives "Watering Ground floor" (First, Second). Receiver "All admins", Send = Yes. OFF rows create nothing. |
| `GetSwitchStatesPlugin` | Plugin of the Custom API **`cr_GetSwitchStates`** (unbound function) | One query, newest rows first, newest row per switch wins. Outputs `Motor`, `GroundFloor`, `FirstFloor`, `SecondFloor`. The web API can call it with `DATAVERSE_READ=customapi`. |
| `SendWhatsAppMessagePlugin` | Plugin of the Custom API **`cr_SendWhatsAppMessage`** (unbound action) | Input `MessageId`. Reads the row and the WhatsApp environment variables, sends one HTTPS POST per recipient to the Meta WhatsApp Cloud API, writes Delivery status and Result back, sets Send = No. Outputs `SentCount`, `FailedCount`, `Result`. |

## Files

```text
plugins/
├── SmartWatering.Plugins/
│   ├── SmartWatering.Plugins.csproj   net462, signs only when key.snk exists
│   ├── Schema.cs                      every table, column and environment variable name
│   ├── SwitchCommandNotifyPlugin.cs
│   ├── GetSwitchStatesPlugin.cs
│   ├── SendWhatsAppMessagePlugin.cs
│   ├── EnvironmentVariableReader.cs   reads environment variables (and secret ones)
│   ├── WhatsAppClient.cs              HttpClient, 15 s timeout, TLS 1.2
│   └── Logic/                         pure logic, no Dataverse types
│       ├── Switches.cs                choice values and alert texts
│       ├── SwitchStates.cs            newest row per switch -> four booleans
│       ├── PhoneNumbers.cs            parse, validate and mask mobile numbers
│       └── WhatsAppPayload.cs         Meta URL, text and template JSON, response parsing
└── SmartWatering.Plugins.Tests/       net8.0 + xunit, links the Logic files
```

## Build and test

You need any recent .NET SDK (8 or newer). The .NET Framework 4.6.2 reference assemblies come from NuGet, so no targeting pack is needed.

```bash
cd plugins/SmartWatering.Plugins
dotnet build -c Release          # 0 errors, 0 warnings; XML docs on every public member

cd ../SmartWatering.Plugins.Tests
dotnet test -c Release           # 41 tests
```

The tests cover number parsing and masking, the Meta payloads (checked as real JSON), the URL checks, the response parsing, the alert texts and the newest-row-per-switch rule. The parts that need Dataverse or Meta (the plugin entry points, `EnvironmentVariableReader`, `WhatsAppClient`) are not unit tested; test them in a demo environment as described in [../docs/setup.md](../docs/setup.md).

## Strong-name key

Classic plugin assemblies must be strong-named. The project signs the assembly only when a file called `key.snk` sits next to the `.csproj`, so the repo builds without one. Create it once:

```bash
cd plugins/SmartWatering.Plugins
sn -k key.snk
```

`sn` comes with the Visual Studio Developer Command Prompt. Without Visual Studio, any tool that writes a 1024 or 2048 bit RSA key pair in SNK format works. `*.snk` is git-ignored: **never commit it**, and keep a private copy, because a new key means re-registering the assembly.

## Register

Use the Plugin Registration Tool (`pac tool prt` from the Power Platform CLI) in your **demo** environment:

1. **Register New Assembly** > `bin/Release/net462/SmartWatering.Plugins.dll`, isolation mode Sandbox, storage Database.
2. On `SmartWatering.Plugins.SwitchCommandNotifyPlugin`, **Register New Step**: Message `Create`, Primary Entity `cr_switchcommand`, Stage PostOperation, Execution Mode **Asynchronous**, filtering attributes not needed for Create.
3. Create the two Custom APIs (by hand or with `dataverse/create-schema.mjs`) and set their **Plugin Type**:
   - `cr_GetSwitchStates` -> `SmartWatering.Plugins.GetSwitchStatesPlugin`
   - `cr_SendWhatsAppMessage` -> `SmartWatering.Plugins.SendWhatsAppMessagePlugin`

Custom API plugins need no step of their own.

## Design notes

- **Who the plugins run as.** `SwitchCommandNotifyPlugin` creates the message row as SYSTEM, so app users and the schedule flow need no rights on the message table and cannot write their own messages. The other two run as the caller: the web API's application user needs read on Switch Command, and the send flow's connection user needs read and write on Send WhatsApp Message and read on environment variables.
- **No loops.** The send plugin sets Send back to No. The flow only fires when Send changes to Yes (see [../flows/send-whatsapp-message.md](../flows/send-whatsapp-message.md)), so the plugin's own update does not start it again. A row already marked Sent is skipped.
- **Time limit.** Plugins have 2 minutes. Each send has a 15 second timeout, and after 90 seconds the plugin stops starting new sends and marks the rest "skipped, time budget used". For a long admin list, split the work.
- **No secrets in code.** Phone number ID, token, numbers and template come from environment variables. The token is never traced, and numbers are masked in the Result column (`91******1234`).
- **Publisher prefix.** Every name lives in `Schema.cs`. If your prefix is not `cr`, change it there and rebuild.

## The 24 hour rule

Meta delivers a free-form text message only inside the **customer service window**: the 24 hours after the recipient last wrote to your WhatsApp business number. An alert to an admin who has not written to you recently is a business-initiated message and must use an **approved template**. So:

- Leave `cr_WhatsAppTemplateName` empty only for testing, after you have messaged the business number from the admin phone.
- For real alerts, create a Utility template in WhatsApp Manager, for example name `watering_alert`, body `Smart watering: {{1}}`, wait for approval, then put the name in `cr_WhatsAppTemplateName` and the language code (for example `en`) in `cr_WhatsAppTemplateLanguage`. The plugin sends the message text as `{{1}}`.

Outside the window a text message fails with Meta error `131047` ("Re-engagement message"). Meta often accepts the request first and reports that failure later through a status webhook. This sample does not listen to webhooks, so **"sent" in the Result column means Meta accepted the message, not that it reached the phone**. Errors Meta returns straight away (wrong token, wrong phone number ID, unknown template) are written to the Result column.
