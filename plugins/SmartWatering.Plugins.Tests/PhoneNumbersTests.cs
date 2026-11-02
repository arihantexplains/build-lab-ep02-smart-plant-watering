using SmartWatering.Plugins.Logic;
using Xunit;

namespace SmartWatering.Plugins.Tests
{
    public class PhoneNumbersTests
    {
        [Theory]
        [InlineData("919800000001", "919800000001")]
        [InlineData("+91 98000 00001", "919800000001")]
        [InlineData("0091-9800-000001", "919800000001")]
        [InlineData(" (91) 98000.00001 ", "919800000001")]
        [InlineData("14155550100", "14155550100")]
        public void Accepts_numbers_with_a_country_code(string raw, string expected)
        {
            Assert.True(PhoneNumbers.TryNormalise(raw, out string digits, out string reason), reason);
            Assert.Equal(expected, digits);
        }

        [Theory]
        [InlineData("", "is empty")]
        [InlineData("9800000001", "is too short")]
        [InlineData("09800000001", "starts with 0")]
        [InlineData("91980000000A", "characters other than digits")]
        [InlineData("91+9800000001", "characters other than digits")]
        [InlineData("9198000000011234", "longer than 15")]
        public void Refuses_bad_numbers(string raw, string reasonPart)
        {
            Assert.False(PhoneNumbers.TryNormalise(raw, out string digits, out string reason));
            Assert.Null(digits);
            Assert.Contains(reasonPart, reason);
        }

        [Fact]
        public void Parses_a_comma_separated_list_and_drops_duplicates()
        {
            var list = PhoneNumbers.ParseList("919800000001, +91 98000 00002;919800000001\n12345");
            Assert.Equal(new[] { "919800000001", "919800000002" }, list.Valid);
            Assert.Single(list.Rejected);
            Assert.Contains("too short", list.Rejected[0]);
        }

        [Fact]
        public void Empty_list_gives_no_recipients()
        {
            var list = PhoneNumbers.ParseList("  ");
            Assert.Empty(list.Valid);
            Assert.Empty(list.Rejected);
        }

        [Theory]
        [InlineData("919800000001", "91******0001")]
        [InlineData("12345", "***")]
        [InlineData(null, "***")]
        public void Masks_the_middle(string value, string expected)
        {
            Assert.Equal(expected, PhoneNumbers.Mask(value));
        }

        [Fact]
        public void Rejected_entries_are_masked()
        {
            var list = PhoneNumbers.ParseList("9800000001980011");
            Assert.DoesNotContain("9800000001980011", list.Rejected[0]);
        }
    }
}
