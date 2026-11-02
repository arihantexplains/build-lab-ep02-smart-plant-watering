namespace SmartWatering.Plugins
{
    /// <summary>
    /// Every Dataverse logical name the plugins use, in one place.
    /// The sample uses the publisher prefix "cr". If your publisher prefix is different, change it here,
    /// rebuild, and use the same names in dataverse/schema.json.
    /// </summary>
    public static class Schema
    {
        /// <summary>Switch Command table: one row per ON/OFF order.</summary>
        public const string SwitchCommandTable = "cr_switchcommand";

        /// <summary>Switch Command: primary name column.</summary>
        public const string SwitchCommandName = "cr_name";

        /// <summary>Switch Command: Switch choice column (Motor and the three floor solenoids).</summary>
        public const string SwitchCommandSwitch = "cr_switch";

        /// <summary>Switch Command: Status yes/no column (Yes = ON).</summary>
        public const string SwitchCommandStatus = "cr_status";

        /// <summary>Send WhatsApp Message table: one row per message.</summary>
        public const string MessageTable = "cr_sendwhatsappmessage";

        /// <summary>Send WhatsApp Message: primary name column.</summary>
        public const string MessageName = "cr_name";

        /// <summary>Send WhatsApp Message: the text to send.</summary>
        public const string MessageText = "cr_messagetext";

        /// <summary>Send WhatsApp Message: who or what created the message, for example "Smart watering".</summary>
        public const string MessageFrom = "cr_from";

        /// <summary>Send WhatsApp Message: receiver name, or "All admins" (or empty) for the admin list.</summary>
        public const string MessageReceiver = "cr_receiver";

        /// <summary>Send WhatsApp Message: receiver mobile number; when filled, only this number gets the message.</summary>
        public const string MessageReceiverNumber = "cr_receivernumber";

        /// <summary>Send WhatsApp Message: Send yes/no column. Yes starts the flow.</summary>
        public const string MessageSend = "cr_send";

        /// <summary>Send WhatsApp Message: Delivery status choice column.</summary>
        public const string MessageDeliveryStatus = "cr_deliverystatus";

        /// <summary>Send WhatsApp Message: result text, one line per recipient (numbers masked).</summary>
        public const string MessageResult = "cr_result";

        /// <summary>Send WhatsApp Message: when the plugin last tried to send.</summary>
        public const string MessageSentOn = "cr_senton";

        /// <summary>Environment variable: admin mobile numbers, comma separated, country code first.</summary>
        public const string EnvAdminNumbers = "cr_WhatsAppAdminNumbers";

        /// <summary>Environment variable: WhatsApp phone number ID from the Meta app.</summary>
        public const string EnvPhoneNumberId = "cr_WhatsAppPhoneNumberId";

        /// <summary>Environment variable: Meta access token (a Secret type variable backed by Azure Key Vault is the safer choice).</summary>
        public const string EnvAccessToken = "cr_WhatsAppAccessToken";

        /// <summary>Environment variable: Graph API version, for example v21.0.</summary>
        public const string EnvApiVersion = "cr_WhatsAppApiVersion";

        /// <summary>Environment variable: approved template name. Empty means send a plain text message.</summary>
        public const string EnvTemplateName = "cr_WhatsAppTemplateName";

        /// <summary>Environment variable: template language code, for example en.</summary>
        public const string EnvTemplateLanguage = "cr_WhatsAppTemplateLanguage";

        /// <summary>Custom API input parameter of cr_SendWhatsAppMessage.</summary>
        public const string ParamMessageId = "MessageId";

        /// <summary>Custom API output of cr_SendWhatsAppMessage: messages Meta accepted.</summary>
        public const string OutSentCount = "SentCount";

        /// <summary>Custom API output of cr_SendWhatsAppMessage: messages that failed or were skipped.</summary>
        public const string OutFailedCount = "FailedCount";

        /// <summary>Custom API output of cr_SendWhatsAppMessage: the same text written to the Result column.</summary>
        public const string OutResult = "Result";

        /// <summary>Custom API output of cr_GetSwitchStates.</summary>
        public const string OutMotor = "Motor";

        /// <summary>Custom API output of cr_GetSwitchStates.</summary>
        public const string OutGroundFloor = "GroundFloor";

        /// <summary>Custom API output of cr_GetSwitchStates.</summary>
        public const string OutFirstFloor = "FirstFloor";

        /// <summary>Custom API output of cr_GetSwitchStates.</summary>
        public const string OutSecondFloor = "SecondFloor";
    }
}
