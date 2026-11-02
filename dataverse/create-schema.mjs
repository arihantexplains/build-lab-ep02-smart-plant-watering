// create-schema.mjs: creates the Smart Watering tables, columns, choices, environment variables and
// Custom APIs through the Dataverse Web API, from schema.json. Node 18+, no packages.
//
// UNTESTED AGAINST A LIVE ENVIRONMENT. Run it in a demo environment only, and read dataverse/README.md first.
// Use --dry-run to print every request without signing in or changing anything.
//
//   node create-schema.mjs --dry-run
//   node create-schema.mjs
//
// Settings come from the environment (never from a file in Git):
//   TENANT_ID, CLIENT_ID, CLIENT_SECRET   an app registration whose application user is System Customizer
//                                         (or System Administrator) in the DEMO environment
//   DATAVERSE_URL                         https://<your-org>.crm.dynamics.com (no trailing slash)
//   SOLUTION                              unique name of an existing unmanaged solution (default from schema.json)
// Optional current values for environment variables (left empty when not set):
//   WA_ADMIN_NUMBERS, WA_PHONE_NUMBER_ID, WA_TEMPLATE_NAME
//   WA_TOKEN_KEYVAULT_REF                 Key Vault secret reference for the access token:
//                                         /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/<vault>/secrets/<name>

import { readFile } from 'node:fs/promises';

const env = process.env;
const DRY = process.argv.includes('--dry-run');
const schema = JSON.parse(await readFile(new URL('./schema.json', import.meta.url), 'utf8'));
const SOLUTION = env.SOLUTION || schema.solutionUniqueName;
const BASE = DRY ? 'https://<your-org>.crm.dynamics.com' : (env.DATAVERSE_URL || '').replace(/\/+$/, '');
const API = `${BASE}/api/data/v9.2`;

if (!DRY) {
  const missing = ['TENANT_ID', 'CLIENT_ID', 'CLIENT_SECRET', 'DATAVERSE_URL'].filter(k => !env[k]);
  if (missing.length) {
    console.error(`Missing settings: ${missing.join(', ')}. See dataverse/README.md, or run with --dry-run.`);
    process.exit(1);
  }
}

// ---------------- helpers ----------------
const label = text => ({
  '@odata.type': 'Microsoft.Dynamics.CRM.Label',
  LocalizedLabels: [{ '@odata.type': 'Microsoft.Dynamics.CRM.LocalizedLabel', Label: text, LanguageCode: 1033 }],
});
const required = r => ({ Value: r ? 'ApplicationRequired' : 'None' });
const ENV_TYPES = { String: 100000000, Number: 100000001, Boolean: 100000002, JSON: 100000003, Secret: 100000005 };
const API_TYPES = { Boolean: 0, DateTime: 1, Decimal: 2, Entity: 3, EntityCollection: 4, EntityReference: 5, Float: 6, Integer: 7, Money: 8, Picklist: 9, String: 10, StringArray: 11, Guid: 12 };

let token = '';
async function getToken() {
  const res = await fetch(`https://login.microsoftonline.com/${env.TENANT_ID}/oauth2/v2.0/token`, {
    method: 'POST',
    body: new URLSearchParams({ grant_type: 'client_credentials', client_id: env.CLIENT_ID, client_secret: env.CLIENT_SECRET, scope: `${BASE}/.default` }),
  });
  if (!res.ok) throw new Error(`Sign-in failed: token ${res.status}. Check TENANT_ID, CLIENT_ID and CLIENT_SECRET.`);
  return (await res.json()).access_token;
}

async function call(method, path, body) {
  const url = path.startsWith('http') ? path : `${API}/${path}`;
  if (DRY) {
    console.log(`${method} ${url}`);
    if (body) console.log('  ' + JSON.stringify(body).slice(0, 300) + (JSON.stringify(body).length > 300 ? ' ...' : ''));
    return { ok: true, json: { value: [] }, id: 'DRY-RUN-ID' };
  }
  const res = await fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json; charset=utf-8',
      'OData-Version': '4.0',
      'OData-MaxVersion': '4.0',
      'MSCRM.SolutionUniqueName': SOLUTION,
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  const json = text ? JSON.parse(text) : {};
  const entityId = res.headers.get('OData-EntityId') || '';
  const id = (entityId.match(/\(([0-9a-fA-F-]{36})\)/) || [])[1];
  if (!res.ok) {
    const msg = json?.error?.message || text.slice(0, 300);
    return { ok: false, status: res.status, message: msg };
  }
  return { ok: true, json, id };
}

const exists = async path => !DRY && (await call('GET', path)).ok;
const step = (ok, what, extra = '') => console.log(`${ok ? 'ok  ' : 'FAIL'} ${what}${extra ? ': ' + extra : ''}`);

// ---------------- columns ----------------
function columnBody(c) {
  const common = { SchemaName: c.schemaName, DisplayName: label(c.displayName), Description: label(c.description || ''), RequiredLevel: required(c.required) };
  switch (c.type) {
    case 'text':
      return { '@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata', ...common, MaxLength: c.maxLength || 100, FormatName: { Value: 'Text' } };
    case 'memo':
      return { '@odata.type': 'Microsoft.Dynamics.CRM.MemoAttributeMetadata', ...common, MaxLength: c.maxLength || 2000, Format: 'TextArea' };
    case 'datetime':
      return { '@odata.type': 'Microsoft.Dynamics.CRM.DateTimeAttributeMetadata', ...common, Format: 'DateAndTime', DateTimeBehavior: { Value: 'UserLocal' } };
    case 'yesno':
      return {
        '@odata.type': 'Microsoft.Dynamics.CRM.BooleanAttributeMetadata', ...common, DefaultValue: !!c.defaultValue,
        OptionSet: {
          '@odata.type': 'Microsoft.Dynamics.CRM.BooleanOptionSetMetadata', OptionSetType: 'Boolean',
          TrueOption: { Value: 1, Label: label(c.trueLabel || 'Yes') },
          FalseOption: { Value: 0, Label: label(c.falseLabel || 'No') },
        },
      };
    case 'choice':
      return {
        '@odata.type': 'Microsoft.Dynamics.CRM.PicklistAttributeMetadata', ...common,
        ...(c.defaultValue !== undefined ? { DefaultFormValue: c.defaultValue } : {}),
        OptionSet: {
          '@odata.type': 'Microsoft.Dynamics.CRM.OptionSetMetadata', IsGlobal: false, OptionSetType: 'Picklist',
          Options: c.options.map(o => ({ Value: o.value, Label: label(o.label) })),
        },
      };
    default:
      throw new Error(`Unknown column type ${c.type} for ${c.schemaName}`);
  }
}

// ---------------- run ----------------
console.log(`${DRY ? 'DRY RUN, nothing is sent. ' : ''}Solution: ${SOLUTION}`);
if (!DRY) token = await getToken();

// 1. Tables and columns
for (const t of schema.tables) {
  if (await exists(`EntityDefinitions(LogicalName='${t.logicalName}')?$select=LogicalName`)) {
    step(true, `table ${t.logicalName}`, 'already there');
  } else {
    const r = await call('POST', 'EntityDefinitions', {
      '@odata.type': 'Microsoft.Dynamics.CRM.EntityMetadata',
      SchemaName: t.schemaName,
      DisplayName: label(t.displayName),
      DisplayCollectionName: label(t.pluralName),
      Description: label(t.description),
      OwnershipType: 'UserOwned',
      IsActivity: false,
      HasActivities: false,
      HasNotes: false,
      Attributes: [{
        '@odata.type': 'Microsoft.Dynamics.CRM.StringAttributeMetadata',
        SchemaName: t.primaryName.schemaName, IsPrimaryName: true, MaxLength: t.primaryName.maxLength,
        FormatName: { Value: 'Text' }, RequiredLevel: required(false),
        DisplayName: label(t.primaryName.displayName), Description: label('Primary name'),
      }],
    });
    step(r.ok, `table ${t.logicalName}`, r.message);
    if (!r.ok) process.exit(1);
  }

  for (const c of t.columns) {
    const logical = c.schemaName.toLowerCase();
    if (await exists(`EntityDefinitions(LogicalName='${t.logicalName}')/Attributes(LogicalName='${logical}')?$select=LogicalName`)) {
      step(true, `  column ${logical}`, 'already there');
      continue;
    }
    const r = await call('POST', `EntityDefinitions(LogicalName='${t.logicalName}')/Attributes`, columnBody(c));
    step(r.ok, `  column ${logical}`, r.message);
  }
}

// 2. Environment variables (values only when given in the environment of this script)
for (const v of schema.environmentVariables) {
  let value = v.valueFrom ? env[v.valueFrom] : undefined;
  let type = v.type;
  if (type === 'Secret' && !value) {
    type = 'String';   // without a Key Vault reference, create a plain variable and fill it in the maker portal
    console.log(`note ${v.schemaName}: no ${v.valueFrom} set, created as String with no value. Prefer Secret + Key Vault.`);
  }
  const found = DRY ? null : (await call('GET', `environmentvariabledefinitions?$select=environmentvariabledefinitionid&$filter=schemaname eq '${v.schemaName}'`)).json?.value?.[0];
  let defId = found?.environmentvariabledefinitionid;
  if (defId) {
    step(true, `env var ${v.schemaName}`, 'already there');
  } else {
    const r = await call('POST', 'environmentvariabledefinitions', {
      schemaname: v.schemaName, displayname: v.displayName, description: v.description,
      type: ENV_TYPES[type], ...(v.defaultValue ? { defaultvalue: v.defaultValue } : {}),
    });
    step(r.ok, `env var ${v.schemaName}`, r.message);
    defId = r.id;
  }
  if (value && defId) {
    const r = await call('POST', 'environmentvariablevalues', {
      schemaname: `${v.schemaName}_value`, value,
      'EnvironmentVariableDefinitionId@odata.bind': `/environmentvariabledefinitions(${defId})`,
    });
    step(r.ok, `  value for ${v.schemaName}`, r.message);
  }
}

// 3. Custom APIs (linked to the plugin types when the assembly is already registered)
for (const a of schema.customApis) {
  if (!DRY && (await call('GET', `customapis?$select=customapiid&$filter=uniquename eq '${a.uniqueName}'`)).json?.value?.length) {
    step(true, `custom API ${a.uniqueName}`, 'already there');
    continue;
  }
  const type = DRY ? null : (await call('GET', `plugintypes?$select=plugintypeid&$filter=typename eq '${a.pluginType}'`)).json?.value?.[0];
  if (!type && !DRY) console.log(`note ${a.uniqueName}: plugin type ${a.pluginType} not registered yet. Set it on the Custom API after registering the assembly.`);
  const r = await call('POST', 'customapis', {
    uniquename: a.uniqueName, name: a.uniqueName, displayname: a.displayName, description: a.description,
    bindingtype: 0, isfunction: a.isFunction, isprivate: false, allowedcustomprocessingsteptype: 0,
    ...(type ? { 'PluginTypeId@odata.bind': `/plugintypes(${type.plugintypeid})` } : {}),
    CustomAPIRequestParameters: a.requestParameters.map(p => ({
      uniquename: p.uniqueName, name: `${a.uniqueName}.${p.uniqueName}`, displayname: p.uniqueName,
      description: p.description, type: API_TYPES[p.type], isoptional: !!p.isOptional,
    })),
    CustomAPIResponseProperties: a.responseProperties.map(p => ({
      uniquename: p.uniqueName, name: `${a.uniqueName}.${p.uniqueName}`, displayname: p.uniqueName,
      description: p.description, type: API_TYPES[p.type],
    })),
  });
  step(r.ok, `custom API ${a.uniqueName}`, r.message);
}

// 4. Publish
const pub = await call('POST', 'PublishAllXml', {});
step(pub.ok, 'publish customizations', pub.message);
console.log('Done. Next: register the plugin assembly and the SwitchCommandNotifyPlugin step (plugins/README.md).');
