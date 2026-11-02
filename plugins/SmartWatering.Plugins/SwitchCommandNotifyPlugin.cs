using System;
using Microsoft.Xrm.Sdk;
using SmartWatering.Plugins.Logic;

namespace SmartWatering.Plugins
{
    /// <summary>
    /// Register on: Create of cr_switchcommand, PostOperation (stage 40), Asynchronous.
    /// When a new Switch Command row says ON, it adds a Send WhatsApp Message row
    /// ("Motor started" or "Watering Ground floor" and so on) for all admins with Send = Yes.
    /// That row starts the send flow. OFF rows create nothing.
    /// </summary>
    public sealed class SwitchCommandNotifyPlugin : IPlugin
    {
        /// <summary>Who the alerts say they are from.</summary>
        public const string AlertSender = "Smart watering";

        /// <summary>Plugin entry point.</summary>
        /// <param name="serviceProvider">Services supplied by Dataverse.</param>
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (!string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(context.PrimaryEntityName, Schema.SwitchCommandTable, StringComparison.OrdinalIgnoreCase) ||
                !context.InputParameters.Contains("Target") ||
                !(context.InputParameters["Target"] is Entity target))
            {
                trace.Trace("SwitchCommandNotify: not a Create of {0}, nothing to do.", Schema.SwitchCommandTable);
                return;
            }

            if (target.GetAttributeValue<bool?>(Schema.SwitchCommandStatus) != true)
            {
                trace.Trace("SwitchCommandNotify: Status is OFF, no alert.");
                return;
            }

            var choice = target.GetAttributeValue<OptionSetValue>(Schema.SwitchCommandSwitch);
            string text = choice == null ? null : Switches.AlertText(choice.Value);
            if (text == null)
            {
                trace.Trace("SwitchCommandNotify: unknown or empty Switch value, no alert.");
                return;
            }

            // Created as SYSTEM on purpose: app users and the schedule flow need no rights on the message table,
            // so they cannot write their own WhatsApp messages. Only this plugin creates alert rows for them.
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(null);

            var message = new Entity(Schema.MessageTable);
            message[Schema.MessageName] = text;
            message[Schema.MessageText] = text;
            message[Schema.MessageFrom] = AlertSender;
            message[Schema.MessageReceiver] = Switches.AllAdmins;
            message[Schema.MessageSend] = true;
            message[Schema.MessageDeliveryStatus] = new OptionSetValue(DeliveryStatus.Pending);

            Guid id = service.Create(message);
            trace.Trace("SwitchCommandNotify: created message {0} \"{1}\".", id, text);
        }
    }
}
