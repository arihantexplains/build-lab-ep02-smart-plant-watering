// Smart Watering web API (demo). The ESP32 makes ONE call and gets four booleans back:
//   GET /api/switches   header x-device-key: <DEVICE_KEY>
//   200 { "motor": true, "groundFloor": true, "firstFloor": false, "secondFloor": false }
//
// Behind it, the API reads the Dataverse "Switch Command" table with a service account
// (an app registration, client credentials). The device never sees that account.
//
// MODE=mock       in-memory table, plus POST /api/mock/rows to play the canvas app or the flow
// MODE=dataverse  real table: TENANT_ID, CLIENT_ID, CLIENT_SECRET, DATAVERSE_URL (app settings)
//   DATAVERSE_READ=table      (default) the API queries the Switch Command table itself
//   DATAVERSE_READ=customapi  the API calls the cr_GetSwitchStates Custom API (plugin) instead
//
// Node 18+, no packages. Run: node server.mjs   (PORT, DEVICE_KEY, MODE from the environment)

import http from 'node:http';
import crypto from 'node:crypto';

const env = process.env;
const PORT = Number(env.PORT || 8080);
const MODE = env.MODE || 'mock';
const DEVICE_KEY = env.DEVICE_KEY || 'PASTE_DEVICE_KEY_HERE';
const DATAVERSE_READ = env.DATAVERSE_READ || 'table';
const CUSTOM_API = env.CUSTOM_API || 'cr_GetSwitchStates';   // unbound function, outputs Motor, GroundFloor, FirstFloor, SecondFloor

// A real install must never run with the sample key or a short one.
if (MODE === 'dataverse' && (DEVICE_KEY === 'PASTE_DEVICE_KEY_HERE' || DEVICE_KEY.length < 24)) {
  console.error('DEVICE_KEY is missing or too short (24 characters or more). Refusing to start in dataverse mode.');
  process.exit(1);
}

// Dataverse names: change these to match your table.
const TABLE_SET = env.TABLE_SET || 'cr_switchcommands';   // entity set name (plural)
const COL_SWITCH = env.COL_SWITCH || 'cr_switch';          // choice column
const COL_STATUS = env.COL_STATUS || 'cr_status';          // Yes/No column (true = ON)
// Choice values of the switch column -> keys in the answer
// Motor, Ground floor solenoid, First floor solenoid, Second floor solenoid
const CHOICES = { 100000000: 'motor', 100000001: 'groundFloor', 100000002: 'firstFloor', 100000003: 'secondFloor' };
const KEYS = Object.values(CHOICES);

// ---------------- mock table ----------------
const mockRows = [];   // { switch: 'motor', on: true, createdOn: ISO, source: 'App' }

function latestFromRows(rows) {
  // rows newest first; the first row seen for each switch wins
  const out = Object.fromEntries(KEYS.map(k => [k, false]));
  const seen = new Set();
  for (const r of rows) {
    if (!seen.has(r.switch) && r.switch in out) { out[r.switch] = !!r.on; seen.add(r.switch); }
  }
  return out;
}

// ---------------- Dataverse with a service account ----------------
let token = { value: '', expires: 0 };

async function getToken() {
  if (token.value && Date.now() < token.expires - 60000) return token.value;
  const url = env.TOKEN_URL || `https://login.microsoftonline.com/${env.TENANT_ID}/oauth2/v2.0/token`;
  const body = new URLSearchParams({
    grant_type: 'client_credentials',
    client_id: env.CLIENT_ID,
    client_secret: env.CLIENT_SECRET,
    scope: `${env.DATAVERSE_URL}/.default`,
  });
  const res = await fetch(url, { method: 'POST', body });
  if (!res.ok) throw new Error(`token ${res.status}`);
  const j = await res.json();
  token = { value: j.access_token, expires: Date.now() + j.expires_in * 1000 };
  return token.value;
}

async function dataverseGet(path) {
  const res = await fetch(`${env.DATAVERSE_URL}/api/data/v9.2/${path}`, {
    headers: { Authorization: `Bearer ${await getToken()}`, Accept: 'application/json', 'OData-Version': '4.0' },
  });
  if (!res.ok) throw new Error(`dataverse ${res.status}`);
  return res.json();
}

async function latestFromCustomApi() {
  // The plugin does the "newest row per switch" work and answers with four booleans.
  const j = await dataverseGet(`${CUSTOM_API}()`);
  return { motor: j.Motor === true, groundFloor: j.GroundFloor === true, firstFloor: j.FirstFloor === true, secondFloor: j.SecondFloor === true };
}

async function latestFromDataverse() {
  if (DATAVERSE_READ === 'customapi') return latestFromCustomApi();
  // ONE query: newest rows first, then the first row per switch wins.
  const q = `$select=${COL_SWITCH},${COL_STATUS},createdon&$orderby=createdon desc&$top=50`;
  const j = await dataverseGet(`${TABLE_SET}?${q}`);
  return latestFromRows(j.value.map(r => ({ switch: CHOICES[r[COL_SWITCH]], on: r[COL_STATUS] === true })));
}

// ---------------- HTTP ----------------
function keyOk(req) {
  const a = Buffer.from(String(req.headers['x-device-key'] || ''));
  const b = Buffer.from(DEVICE_KEY);
  return a.length === b.length && crypto.timingSafeEqual(a, b);
}

function send(res, code, obj) {
  res.writeHead(code, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(obj));
}

function readJson(req) {
  return new Promise((ok, bad) => {
    let s = '';
    req.on('data', c => { s += c; if (s.length > 10000) req.destroy(); });
    req.on('end', () => { try { ok(JSON.parse(s || '{}')); } catch (e) { bad(e); } });
  });
}

export const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://x');
  try {
    if (req.method === 'GET' && url.pathname === '/api/switches') {
      if (!keyOk(req)) return send(res, 401, { error: 'device key' });
      const state = MODE === 'dataverse' ? await latestFromDataverse() : latestFromRows(mockRows);
      return send(res, 200, state);
    }
    if (MODE === 'mock' && url.pathname === '/api/mock/rows') {
      if (!keyOk(req)) return send(res, 401, { error: 'device key' });
      if (req.method === 'GET') return send(res, 200, mockRows);
      if (req.method === 'POST') {
        const b = await readJson(req);
        if (!KEYS.includes(b.switch)) return send(res, 400, { error: `switch must be one of ${KEYS.join(', ')}` });
        const row = { switch: b.switch, on: !!b.on, createdOn: new Date().toISOString(), source: b.source || 'App' };
        mockRows.unshift(row);
        return send(res, 201, row);
      }
    }
    if (url.pathname === '/health') return send(res, 200, { ok: true, mode: MODE });
    send(res, 404, { error: 'not found' });
  } catch (e) {
    console.error(new Date().toISOString(), e.message);
    send(res, 502, { error: 'upstream' });   // the ESP32 counts this as a failed poll
  }
});

if (process.argv[1] && process.argv[1].endsWith('server.mjs')) {
  server.listen(PORT, () => console.log(`Smart Watering API on :${PORT} (${MODE}${MODE === 'dataverse' ? `, read: ${DATAVERSE_READ}` : ''})`));
}
