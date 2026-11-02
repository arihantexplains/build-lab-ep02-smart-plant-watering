using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartWatering.Plugins.Logic
{
    /// <summary>
    /// Builds the request for the Meta WhatsApp Cloud API and reads its answer.
    /// Plain string building, so it works on .NET Framework 4.6.2 without a JSON package and can be unit tested.
    /// </summary>
    public static class WhatsAppPayload
    {
        /// <summary>Longest text body the Cloud API accepts.</summary>
        public const int MaxTextLength = 4096;

        private static readonly Regex VersionPattern = new Regex(@"^v\d{1,3}(\.\d{1,2})?$", RegexOptions.CultureInvariant);
        private static readonly Regex DigitsPattern = new Regex(@"^\d{5,20}$", RegexOptions.CultureInvariant);
        private static readonly Regex TemplateNamePattern = new Regex(@"^[a-z0-9_]{1,512}$", RegexOptions.CultureInvariant);
        private static readonly Regex MessageIdPattern = new Regex("\"id\"\\s*:\\s*\"(wamid\\.[^\"]+)\"", RegexOptions.CultureInvariant);
        private static readonly Regex ErrorMessagePattern = new Regex("\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);
        private static readonly Regex ErrorCodePattern = new Regex("\"code\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);

        /// <summary>
        /// The send endpoint: https://graph.facebook.com/{version}/{phone-number-id}/messages.
        /// </summary>
        /// <param name="apiVersion">Graph API version, for example "v21.0".</param>
        /// <param name="phoneNumberId">The WhatsApp phone number ID from the Meta app (digits, not the phone number itself).</param>
        /// <exception cref="ArgumentException">When the version or the phone number ID has the wrong format.</exception>
        public static string MessagesUrl(string apiVersion, string phoneNumberId)
        {
            string version = (apiVersion ?? string.Empty).Trim();
            string id = (phoneNumberId ?? string.Empty).Trim();
            if (!VersionPattern.IsMatch(version))
                throw new ArgumentException("The Graph API version must look like v21.0.", nameof(apiVersion));
            if (!DigitsPattern.IsMatch(id))
                throw new ArgumentException("The WhatsApp phone number ID must be digits only.", nameof(phoneNumberId));
            return "https://graph.facebook.com/" + version + "/" + id + "/messages";
        }

        /// <summary>
        /// A free-form text message. Meta only delivers these inside the 24 hour customer service window,
        /// that is, after the recipient has written to your business number in the last 24 hours.
        /// </summary>
        /// <param name="to">Recipient, digits only with the country code.</param>
        /// <param name="body">Message text; longer text is cut to 4096 characters.</param>
        public static string Text(string to, string body)
        {
            string text = body ?? string.Empty;
            if (text.Length > MaxTextLength) text = text.Substring(0, MaxTextLength);
            return "{\"messaging_product\":\"whatsapp\",\"recipient_type\":\"individual\",\"to\":" + JsonString(to) +
                   ",\"type\":\"text\",\"text\":{\"preview_url\":false,\"body\":" + JsonString(text) + "}}";
        }

        /// <summary>
        /// A template message. Business-initiated messages (an alert to an admin who has not written in the last
        /// 24 hours) must use a template that Meta has approved.
        /// </summary>
        /// <param name="to">Recipient, digits only with the country code.</param>
        /// <param name="templateName">Approved template name, lowercase letters, digits and underscores.</param>
        /// <param name="languageCode">Template language code, for example "en" or "en_US".</param>
        /// <param name="bodyParameter">Value for the template's {{1}} placeholder, or null for a template without one.</param>
        /// <exception cref="ArgumentException">When the template name has the wrong format.</exception>
        public static string Template(string to, string templateName, string languageCode, string bodyParameter)
        {
            string name = (templateName ?? string.Empty).Trim();
            if (!TemplateNamePattern.IsMatch(name))
                throw new ArgumentException("The template name must be lowercase letters, digits and underscores.", nameof(templateName));
            string language = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode.Trim();

            var sb = new StringBuilder();
            sb.Append("{\"messaging_product\":\"whatsapp\",\"recipient_type\":\"individual\",\"to\":").Append(JsonString(to));
            sb.Append(",\"type\":\"template\",\"template\":{\"name\":").Append(JsonString(name));
            sb.Append(",\"language\":{\"code\":").Append(JsonString(language)).Append('}');
            if (bodyParameter != null)
            {
                sb.Append(",\"components\":[{\"type\":\"body\",\"parameters\":[{\"type\":\"text\",\"text\":")
                  .Append(JsonString(CleanTemplateParameter(bodyParameter)))
                  .Append("}]}]");
            }
            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>
        /// Template parameters may not contain new lines, tabs or more than four spaces in a row,
        /// so those are turned into single spaces.
        /// </summary>
        /// <param name="value">The raw parameter value.</param>
        public static string CleanTemplateParameter(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string oneLine = Regex.Replace(value, @"[\r\n\t]+", " ");
            oneLine = Regex.Replace(oneLine, @" {2,}", " ");
            return oneLine.Trim();
        }

        /// <summary>Writes a value as a JSON string literal, quotes included.</summary>
        /// <param name="value">Any text; null becomes an empty string.</param>
        public static string JsonString(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>Reads the message ID (wamid...) from a successful send response.</summary>
        /// <param name="responseJson">Body of the Cloud API response.</param>
        /// <returns>The message ID, or null when there is none.</returns>
        public static string TryReadMessageId(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson)) return null;
            var m = MessageIdPattern.Match(responseJson);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// Turns an error response into one short line, for example "code 131047: Re-engagement message".
        /// </summary>
        /// <param name="responseJson">Body of the Cloud API error response.</param>
        public static string DescribeError(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson)) return "no details in the response";
            var code = ErrorCodePattern.Match(responseJson);
            var message = ErrorMessagePattern.Match(responseJson);
            if (message.Success)
            {
                string text = message.Groups[1].Value;
                try { text = Regex.Unescape(text); }
                catch (ArgumentException) { /* keep the escaped text */ }
                return code.Success ? "code " + code.Groups[1].Value + ": " + text : text;
            }
            string raw = responseJson.Trim();
            return raw.Length > 200 ? raw.Substring(0, 200) : raw;
        }
    }

    /// <summary>Values of the Delivery status choice column on the Send WhatsApp Message table.</summary>
    public static class DeliveryStatus
    {
        /// <summary>Waiting to be sent.</summary>
        public const int Pending = 100000000;

        /// <summary>Sent to every recipient.</summary>
        public const int Sent = 100000001;

        /// <summary>Sent to some recipients, failed for others.</summary>
        public const int PartlySent = 100000002;

        /// <summary>Nothing was sent.</summary>
        public const int Failed = 100000003;

        /// <summary>Picks the status from the counts.</summary>
        /// <param name="sent">Messages Meta accepted.</param>
        /// <param name="failed">Messages that failed or were skipped.</param>
        public static int From(int sent, int failed)
        {
            if (sent > 0 && failed == 0) return Sent;
            if (sent > 0) return PartlySent;
            return Failed;
        }
    }
}
