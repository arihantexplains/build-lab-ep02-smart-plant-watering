// node test.mjs  -> runs the API in mock mode and in dataverse mode (against a local fake Dataverse + token endpoint)
import http from 'node:http';
import { spawn } from 'node:child_process';
import assert from 'node:assert/strict';

const KEY = 'test-device-key-0123456789abcdef';
let pass = 0;
const ok = (c, m) => { assert.ok(c, m); pass++; console.log('  ok', m); };
const wait = ms => new Promise(r => setTimeout(r, ms));

function start(envExtra, port) {
  const p = spawn(process.execPath, ['server.mjs'], { env: { ...process.env, PORT: String(port), DEVICE_KEY: KEY, ...envExtra }, stdio: 'pipe' });
  return new Promise(r => p.stdout.once('data', () => r(p)));
}
const get = (port, key = KEY) => fetch(`http://127.0.0.1:${port}/api/switches`, { headers: key ? { 'x-device-key': key } : {} });
const post = (port, b, key = KEY) => fetch(`http://127.0.0.1:${port}/api/mock/rows`, { method: 'POST', headers: key ? { 'x-device-key': key } : {}, body: JSON.stringify(b) });

// ---- mock mode ----
console.log('mock mode');
let p = await start({ MODE: 'mock' }, 18081);
ok((await get(18081, null)).status === 401, 'no key -> 401');
ok((await get(18081, 'wrong-device-key-0123456789abcd')).status === 401, 'wrong key -> 401');
ok((await post(18081, { switch: 'motor', on: true }, null)).status === 401, 'mock rows need the key too');
let r = await (await get(18081)).json();
ok(JSON.stringify(r) === '{"motor":false,"groundFloor":false,"firstFloor":false,"secondFloor":false}', 'empty table -> all false');
await post(18081, { switch: 'groundFloor', on: true, source: 'App' }); await wait(5);
await post(18081, { switch: 'motor', on: true, source: 'App' });
r = await (await get(18081)).json();
ok(r.motor && r.groundFloor && !r.firstFloor && !r.secondFloor, 'app ON rows -> motor + groundFloor true');
await wait(5); await post(18081, { switch: 'motor', on: false, source: 'Schedule' });
r = await (await get(18081)).json();
ok(!r.motor && r.groundFloor, 'newest row wins: motor false, groundFloor still true');
ok((await post(18081, { switch: 'pump', on: true })).status === 400, 'unknown switch rejected');
p.kill();

// ---- dataverse mode against a fake ----
console.log('dataverse mode (fake Dataverse + token endpoint)');
let tokenCalls = 0, dvCalls = 0, lastAuth = '', lastQuery = '';
const rows = [
  { cr_switch: 100000001, cr_status: false, createdon: '2026-11-02T06:02:07Z' },   // newest groundFloor = OFF
  { cr_switch: 100000000, cr_status: true, createdon: '2026-11-02T06:02:05Z' },    // newest motor = ON
  { cr_switch: 100000002, cr_status: true, createdon: '2026-11-02T06:02:01Z' },
  { cr_switch: 100000001, cr_status: true, createdon: '2026-11-02T06:00:02Z' },    // older, ignored
];
const fake = http.createServer((req, res) => {
  if (req.url.startsWith('/token')) { tokenCalls++; res.end(JSON.stringify({ access_token: 'tok-abc', expires_in: 3600 })); return; }
  if (req.url.startsWith('/api/data/v9.2/cr_GetSwitchStates()')) { dvCalls++; res.end(JSON.stringify({ Motor: true, GroundFloor: false, FirstFloor: false, SecondFloor: true })); return; }
  if (req.url.startsWith('/api/data/v9.2/cr_switchcommands')) { dvCalls++; lastAuth = req.headers.authorization; lastQuery = decodeURIComponent(req.url); res.end(JSON.stringify({ value: rows })); return; }
  res.statusCode = 404; res.end();
}).listen(18090);
p = await start({ MODE: 'dataverse', TOKEN_URL: 'http://127.0.0.1:18090/token', DATAVERSE_URL: 'http://127.0.0.1:18090', CLIENT_ID: 'x', CLIENT_SECRET: 'y', TENANT_ID: 'z' }, 18082);
r = await (await get(18082)).json();
ok(JSON.stringify(r) === '{"motor":true,"groundFloor":false,"firstFloor":true,"secondFloor":false}', 'latest row per switch -> 4 booleans');
ok(lastAuth === 'Bearer tok-abc', 'service-account token sent to Dataverse');
ok(lastQuery.includes('$orderby=createdon desc'), 'one query, newest first');
await get(18082); await get(18082);
ok(dvCalls === 3 && tokenCalls === 1, 'one Dataverse call per device poll, token cached');
p.kill();

console.log('dataverse mode, read through the Custom API');
const dvEnv = { MODE: 'dataverse', TOKEN_URL: 'http://127.0.0.1:18090/token', DATAVERSE_URL: 'http://127.0.0.1:18090', CLIENT_ID: 'x', CLIENT_SECRET: 'y', TENANT_ID: 'z' };
p = await start({ ...dvEnv, DATAVERSE_READ: 'customapi' }, 18083);
r = await (await get(18083)).json();
ok(JSON.stringify(r) === '{"motor":true,"groundFloor":false,"firstFloor":false,"secondFloor":true}', 'Custom API outputs -> 4 booleans');
ok(dvCalls === 4, 'one Custom API call per device poll');

console.log('safety checks at start');
const refused = spawn(process.execPath, ['server.mjs'], { env: { ...process.env, ...dvEnv, PORT: '18084', DEVICE_KEY: 'PASTE_DEVICE_KEY_HERE' }, stdio: 'pipe' });
const exitCode = await new Promise(r => refused.on('exit', r));
ok(exitCode === 1, 'dataverse mode refuses the sample device key');

fake.close(() => {});
await new Promise(r => fake.closeAllConnections ? (fake.closeAllConnections(), r()) : r());
const down = await get(18083);
ok(down.status === 502, 'Dataverse down -> 502 (device counts a failed poll)');
p.kill();
console.log(`\n${pass} checks passed`);
process.exit(0);
