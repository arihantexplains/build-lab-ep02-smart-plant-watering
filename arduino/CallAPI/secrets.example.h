// secrets.example.h
// Copy this file to secrets.h and fill in your own values.
// secrets.h is listed in .gitignore and must never be committed or shown on screen.
#pragma once

// Home Wi-Fi (2.4 GHz, the ESP32 does not use 5 GHz)
#define WIFI_SSID      "YOUR_WIFI_NAME"
#define WIFI_PASSWORD  "YOUR_WIFI_PASSWORD"

// Your web API host name (Azure App Service), without https:// and without a trailing slash
#define API_HOST       "YOUR-API-NAME.azurewebsites.net"

// Device key issued by the web app (sent in the x-device-key header)
#define DEVICE_KEY     "PASTE_DEVICE_KEY_HERE"

// Root certificate of the web app's HTTPS chain (PEM format).
// For *.azurewebsites.net this is currently DigiCert Global Root G2.
// Check it in your browser (padlock > certificate > top of the chain) and paste it here.
static const char ROOT_CA[] = R"PEM(
-----BEGIN CERTIFICATE-----
PASTE_ROOT_CERTIFICATE_HERE
-----END CERTIFICATE-----
)PEM";
