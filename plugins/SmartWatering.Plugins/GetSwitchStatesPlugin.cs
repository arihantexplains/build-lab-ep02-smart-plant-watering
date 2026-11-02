using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SmartWatering.Plugins.Logic;

namespace SmartWatering.Plugins
{
    /// <summary>
    /// Backs the Custom API cr_GetSwitchStates (unbound function, no inputs).
    /// Outputs four booleans, Motor, GroundFloor, FirstFloor and SecondFloor, from ONE query:
    /// the newest Switch Command rows first, and the newest row per switch wins.
    /// The web API can call this instead of reading the table itself (DATAVERSE_READ=customapi).
    /// </summary>
    public sealed class GetSwitchStatesPlugin : IPlugin
    {
        /// <summary>
        /// How many of the newest rows are read. Four switches with a few rows each fit easily.
        /// A switch whose newest row is older than this many rows reads as OFF, which is the safe side.
        /// </summary>
        public const int RowsToRead = 50;

        /// <summary>Plugin entry point.</summary>
        /// <param name="serviceProvider">Services supplied by Dataverse.</param>
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            // Runs as the caller, so the caller needs read access to the Switch Command table (the web API's
            // application user has exactly that, nothing more).
            var service = factory.CreateOrganizationService(context.UserId);

            var query = new QueryExpression(Schema.SwitchCommandTable)
            {
                ColumnSet = new ColumnSet(Schema.SwitchCommandSwitch, Schema.SwitchCommandStatus, "createdon"),
                TopCount = RowsToRead,
                NoLock = true,
            };
            query.AddOrder("createdon", OrderType.Descending);

            var rows = service.RetrieveMultiple(query).Entities.Select(e => new SwitchReading(
                e.GetAttributeValue<OptionSetValue>(Schema.SwitchCommandSwitch)?.Value,
                e.GetAttributeValue<bool?>(Schema.SwitchCommandStatus) == true));

            var states = SwitchStates.FromNewestFirst(rows);
            context.OutputParameters[Schema.OutMotor] = states.Motor;
            context.OutputParameters[Schema.OutGroundFloor] = states.GroundFloor;
            context.OutputParameters[Schema.OutFirstFloor] = states.FirstFloor;
            context.OutputParameters[Schema.OutSecondFloor] = states.SecondFloor;
            trace.Trace("GetSwitchStates: {0}", states.ToJson());
        }
    }
}
