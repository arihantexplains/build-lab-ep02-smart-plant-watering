using System;
using System.Text.Json;
using SmartWatering.Plugins.Logic;
using Xunit;

namespace SmartWatering.Plugins.Tests
{
    public class WhatsAppPayloadTests
    {
        [Fact]
        public void Builds_the_messages_url()
        {
            Assert.Equal("https://graph.facebook.com/v21.0/123456789012345/messages",
                WhatsAppPayload.MessagesUrl(" v21.0 ", "123456789012345"));
        }

        [Theory]
        [InlineData("21.0", "123456789012345")]
        [InlineData("v21.0/../x", "123456789012345")]
        [InlineData("v21.0", "PASTE_PHONE_NUMBER_ID")]
        [InlineData("v21.0", "")]
        public void Refuses_bad_url_parts(string version, string id)
        {
            Assert.Throws<ArgumentException>(() => WhatsAppPayload.MessagesUrl(version, id));
        }

        [Fact]
        public void Text_payload_is_valid_json_with_the_right_fields()
        {
            string json = WhatsAppPayload.Text("919800000001", "Watering \"Ground\" floor\nnow");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
            Assert.Equal("919800000001", root.GetProperty("to").GetString());
            Assert.Equal("text", root.GetProperty("type").GetString());
            Assert.Equal("Watering \"Ground\" floor\nnow", root.GetProperty("text").GetProperty("body").GetString());
        }

        [Fact]
        public void Text_is_cut_to_the_api_limit()
        {
            string json = WhatsAppPayload.Text("919800000001", new string('a', 5000));
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(WhatsAppPayload.MaxTextLength, doc.RootElement.GetProperty("text").GetProperty("body").GetString().Length);
        }

        [Fact]
        public void Template_payload_carries_name_language_and_parameter()
        {
            string json = WhatsAppPayload.Template("919800000001", "watering_alert", "en", "Motor started\n\tnow    please");
            using var doc = JsonDocument.Parse(json);
            var t = doc.RootElement.GetProperty("template");
            Assert.Equal("template", doc.RootElement.GetProperty("type").GetString());
            Assert.Equal("watering_alert", t.GetProperty("name").GetString());
            Assert.Equal("en", t.GetProperty("language").GetProperty("code").GetString());
            var p = t.GetProperty("components")[0].GetProperty("parameters")[0];
            Assert.Equal("Motor started now please", p.GetProperty("text").GetString());
        }

        [Fact]
        public void Template_without_parameter_has_no_components()
        {
            string json = WhatsAppPayload.Template("919800000001", "hello_world", null, null);
            using var doc = JsonDocument.Parse(json);
            var t = doc.RootElement.GetProperty("template");
            Assert.False(t.TryGetProperty("components", out _));
            Assert.Equal("en", t.GetProperty("language").GetProperty("code").GetString());
        }

        [Fact]
        public void Template_name_must_be_lowercase_snake_case()
        {
            Assert.Throws<ArgumentException>(() => WhatsAppPayload.Template("919800000001", "Watering Alert", "en", "x"));
        }

        [Fact]
        public void Escapes_control_characters()
        {
            Assert.Equal("\"a\\u0001b\\\\c\"", WhatsAppPayload.JsonString("a\u0001b\\c"));
        }

        [Fact]
        public void Reads_the_message_id()
        {
            string body = "{\"messaging_product\":\"whatsapp\",\"contacts\":[{\"input\":\"919800000001\",\"wa_id\":\"919800000001\"}],\"messages\":[{\"id\":\"wamid.HBgMOTE5ODAwMDAwMDAx\"}]}";
            Assert.Equal("wamid.HBgMOTE5ODAwMDAwMDAx", WhatsAppPayload.TryReadMessageId(body));
            Assert.Null(WhatsAppPayload.TryReadMessageId("{}"));
        }

        [Fact]
        public void Describes_a_meta_error()
        {
            string body = "{\"error\":{\"message\":\"Re-engagement message\",\"type\":\"OAuthException\",\"code\":131047}}";
            Assert.Equal("code 131047: Re-engagement message", WhatsAppPayload.DescribeError(body));
            Assert.Equal("no details in the response", WhatsAppPayload.DescribeError(""));
        }
    }
}
