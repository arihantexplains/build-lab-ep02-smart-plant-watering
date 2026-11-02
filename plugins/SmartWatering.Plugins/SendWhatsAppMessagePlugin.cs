using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SmartWatering.Plugins.Logic;

namespace SmartWatering.Plugins
{
    /// <summary>
    /// Backs the Custom API cr_SendWhatsAppMessage (unbound action).
    /// Input: MessageId (Guid of a Send WhatsApp Message row).
    /// Reads the row and the WhatsApp environment variables, works out the recipients (the receiver number when
    /// one is set, otherwise every admin number), sends one HTTPS POST per recipient to the Meta WhatsApp Cloud API,
    /// and writes the result back to the row with Send = No.
    /// Outputs: SentCount, FailedCount, Result.
    /// </summary>
    public sealed class SendWhatsAppMessagePlugin : IPlugin
    {
        /// <summary>Default Graph API version when cr_WhatsAppApiVersion is empty. Check Meta's changelog for the current one.</summary>
        public const string DefaultApiVersion = "v21.0";

        /// <summary>Stop starting new sends after this long, so the plugin stays well inside the 2 minute plugin limit.</summary>
        public static readonly TimeSpan TimeBudget = TimeSpan.FromSeconds(90);

        /// <summary>Longest text written to the Result column.</summary>
        public const int MaxResultLength = 4000;

        /// <summary>Plugin entry point.</summary>
        /// <param name="serviceProvider">Services supplied by Dataverse.</param>
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            if (!context.InputParameters.Contains(Schema.ParamMessageId) ||
                !(context.InputParameters[Schema.ParamMessageId] is Guid messageId) ||
                messageId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("MessageId is required.");
            }

            Entity row;
            try
            {
                row = service.Retrieve(Schema.MessageTable, messageId, new ColumnSet(
                    Schema.MessageText, Schema.MessageReceiver, Schema.MessageReceiverNumber, Schema.MessageDeliveryStatus));
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("Send WhatsApp Message row " + messageId + " was not found or cannot be read. " + ex.Message, ex);
            }

            if (row.GetAttributeValue<OptionSetValue>(Schema.MessageDeliveryStatus)?.Value == DeliveryStatus.Sent)
            {
                Finish(context, service, null, 0, 0, "Already sent, nothing to do.");
                return;
            }

            string text = row.GetAttributeValue<string>(Schema.MessageText);
            if (string.IsNullOrWhiteSpace(text))
            {
                Finish(context, service, messageId, 0, 1, "The message text is empty, nothing was sent.");
                return;
            }

            var env = new EnvironmentVariableReader(service, trace);
            env.Load(Schema.EnvAdminNumbers, Schema.EnvPhoneNumberId, Schema.EnvAccessToken,
                     Schema.EnvApiVersion, Schema.EnvTemplateName, Schema.EnvTemplateLanguage);

            string receiverNumber = row.GetAttributeValue<string>(Schema.MessageReceiverNumber);
            bool singleReceiver = !string.IsNullOrWhiteSpace(receiverNumber);
            RecipientList recipients = singleReceiver
                ? PhoneNumbers.ParseList(receiverNumber)
                : PhoneNumbers.ParseList(env.Optional(Schema.EnvAdminNumbers, null));

            var lines = new List<string>();
            foreach (string rejected in recipients.Rejected) lines.Add(rejected + ", skipped");

            if (recipients.Valid.Count == 0)
            {
                lines.Insert(0, singleReceiver
                    ? "No valid recipient numbers. Check the Receiver number on the row."
                    : "No valid recipient numbers. Check the " + Schema.EnvAdminNumbers + " environment variable.");
                Finish(context, service, messageId, 0, Math.Max(1, recipients.Rejected.Count), string.Join("\n", lines));
                return;
            }

            string url;
            try
            {
                url = WhatsAppPayload.MessagesUrl(env.Optional(Schema.EnvApiVersion, DefaultApiVersion), env.Required(Schema.EnvPhoneNumberId));
            }
            catch (ArgumentException ex)
            {
                throw new InvalidPluginExecutionException("WhatsApp settings are not valid: " + ex.Message, ex);
            }
            string token = env.Required(Schema.EnvAccessToken);
            string template = env.Optional(Schema.EnvTemplateName, null);
            string language = env.Optional(Schema.EnvTemplateLanguage, "en");

            var client = new WhatsAppClient();
            var clock = Stopwatch.StartNew();
            int sent = 0, failed = recipients.Rejected.Count;

            foreach (string to in recipients.Valid)
            {
                string masked = PhoneNumbers.Mask(to);
                if (clock.Elapsed > TimeBudget)
                {
                    failed++;
                    lines.Add(masked + ": skipped, time budget used");
                    continue;
                }

                string payload = string.IsNullOrWhiteSpace(template)
                    ? WhatsAppPayload.Text(to, text)
                    : WhatsAppPayload.Template(to, template, language, text);

                SendOutcome outcome = client.Send(url, token, payload);
                if (outcome.Ok)
                {
                    sent++;
                    lines.Add(masked + ": sent" + (outcome.MessageId != null ? " (" + outcome.MessageId + ")" : string.Empty));
                }
                else
                {
                    failed++;
                    lines.Add(masked + ": failed, " + outcome.Error);
                }
                trace.Trace("SendWhatsAppMessage: {0} {1}", masked, outcome.Ok ? "sent" : "failed " + outcome.StatusCode);
            }

            Finish(context, service, messageId, sent, failed, string.Join("\n", lines));
        }

        private static void Finish(IPluginExecutionContext context, IOrganizationService service, Guid? messageId, int sent, int failed, string result)
        {
            if (result.Length > MaxResultLength) result = result.Substring(0, MaxResultLength);

            if (messageId.HasValue)
            {
                // Send goes back to No so the row cannot fire the flow again by itself.
                var update = new Entity(Schema.MessageTable, messageId.Value);
                update[Schema.MessageDeliveryStatus] = new OptionSetValue(DeliveryStatus.From(sent, failed));
                update[Schema.MessageResult] = result;
                update[Schema.MessageSend] = false;
                update[Schema.MessageSentOn] = DateTime.UtcNow;
                service.Update(update);
            }

            context.OutputParameters[Schema.OutSentCount] = sent;
            context.OutputParameters[Schema.OutFailedCount] = failed;
            context.OutputParameters[Schema.OutResult] = result;
        }
    }
}
