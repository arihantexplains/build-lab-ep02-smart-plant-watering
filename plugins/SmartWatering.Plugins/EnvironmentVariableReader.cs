using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace SmartWatering.Plugins
{
    /// <summary>
    /// Reads Dataverse environment variables from the environmentvariabledefinition and
    /// environmentvariablevalue tables: the current value wins, otherwise the default value.
    /// Secret type variables (stored in Azure Key Vault) are read with the
    /// RetrieveEnvironmentVariableSecretValue message instead, because their value is not in the table.
    /// </summary>
    public sealed class EnvironmentVariableReader
    {
        /// <summary>Type value of a Secret environment variable.</summary>
        public const int SecretType = 100000005;

        private readonly IOrganizationService _service;
        private readonly ITracingService _trace;
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates a reader.</summary>
        /// <param name="service">Organization service used for the query.</param>
        /// <param name="trace">Tracing service. Values are never traced.</param>
        public EnvironmentVariableReader(IOrganizationService service, ITracingService trace)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _trace = trace;
        }

        /// <summary>Loads the given variables with one query (plus one call per secret variable).</summary>
        /// <param name="schemaNames">Schema names, for example cr_WhatsAppPhoneNumberId.</param>
        public void Load(params string[] schemaNames)
        {
            var query = new QueryExpression("environmentvariabledefinition")
            {
                ColumnSet = new ColumnSet("schemaname", "defaultvalue", "type"),
            };
            query.Criteria.AddCondition("schemaname", ConditionOperator.In, (object[])schemaNames);
            var link = query.AddLink("environmentvariablevalue", "environmentvariabledefinitionid", "environmentvariabledefinitionid", JoinOperator.LeftOuter);
            link.EntityAlias = "v";
            link.Columns = new ColumnSet("value");

            foreach (var row in _service.RetrieveMultiple(query).Entities)
            {
                string name = row.GetAttributeValue<string>("schemaname");
                int type = row.GetAttributeValue<OptionSetValue>("type")?.Value ?? 0;
                string value;

                if (type == SecretType)
                {
                    value = ReadSecret(name);
                }
                else
                {
                    var current = row.GetAttributeValue<AliasedValue>("v.value")?.Value as string;
                    value = !string.IsNullOrWhiteSpace(current) ? current : row.GetAttributeValue<string>("defaultvalue");
                }

                if (!string.IsNullOrWhiteSpace(value)) _values[name] = value.Trim();
            }

            _trace?.Trace("EnvironmentVariables: {0} of {1} have a value.", _values.Count, schemaNames.Length);
        }

        /// <summary>Returns a value that must be there.</summary>
        /// <param name="schemaName">Schema name of the variable.</param>
        /// <exception cref="InvalidPluginExecutionException">When the variable is missing or empty.</exception>
        public string Required(string schemaName)
        {
            if (_values.TryGetValue(schemaName, out string value)) return value;
            throw new InvalidPluginExecutionException("Environment variable " + schemaName + " has no value in this environment.");
        }

        /// <summary>Returns a value, or the fallback when the variable is missing or empty.</summary>
        /// <param name="schemaName">Schema name of the variable.</param>
        /// <param name="fallback">Value to use when there is none.</param>
        public string Optional(string schemaName, string fallback)
        {
            return _values.TryGetValue(schemaName, out string value) ? value : fallback;
        }

        private string ReadSecret(string schemaName)
        {
            try
            {
                var request = new OrganizationRequest("RetrieveEnvironmentVariableSecretValue");
                request["EnvironmentVariableName"] = schemaName;
                var response = _service.Execute(request);
                return response.Results.Contains("EnvironmentVariableSecretValue")
                    ? response["EnvironmentVariableSecretValue"] as string
                    : null;
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    "Could not read the secret environment variable " + schemaName + ". Check the Key Vault setup. " + ex.Message, ex);
            }
        }
    }
}
