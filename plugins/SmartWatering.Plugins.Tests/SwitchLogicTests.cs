using SmartWatering.Plugins.Logic;
using Xunit;

namespace SmartWatering.Plugins.Tests
{
    public class SwitchLogicTests
    {
        [Theory]
        [InlineData(Switches.Motor, "Motor started")]
        [InlineData(Switches.GroundFloor, "Watering Ground floor")]
        [InlineData(Switches.FirstFloor, "Watering First floor")]
        [InlineData(Switches.SecondFloor, "Watering Second floor")]
        public void Alert_text_per_switch(int choice, string expected)
        {
            Assert.Equal(expected, Switches.AlertText(choice));
        }

        [Fact]
        public void Unknown_switch_has_no_alert()
        {
            Assert.Null(Switches.AlertText(100000004));
            Assert.Null(Switches.Label(1));
            Assert.False(Switches.IsKnown(100000004));
        }

        [Fact]
        public void Newest_row_per_switch_wins()
        {
            var rows = new[]
            {
                new SwitchReading(Switches.GroundFloor, false),   // newest
                new SwitchReading(Switches.Motor, true),
                new SwitchReading(Switches.FirstFloor, true),
                new SwitchReading(null, true),                    // no switch, ignored
                new SwitchReading(100000009, true),               // unknown, ignored
                new SwitchReading(Switches.GroundFloor, true),    // older, ignored
            };
            var s = SwitchStates.FromNewestFirst(rows);
            Assert.True(s.Motor);
            Assert.False(s.GroundFloor);
            Assert.True(s.FirstFloor);
            Assert.False(s.SecondFloor);
            Assert.Equal("{\"motor\":true,\"groundFloor\":false,\"firstFloor\":true,\"secondFloor\":false}", s.ToJson());
        }

        [Fact]
        public void No_rows_means_everything_off()
        {
            Assert.Equal("{\"motor\":false,\"groundFloor\":false,\"firstFloor\":false,\"secondFloor\":false}",
                SwitchStates.FromNewestFirst(null).ToJson());
        }

        [Theory]
        [InlineData(2, 0, DeliveryStatus.Sent)]
        [InlineData(1, 1, DeliveryStatus.PartlySent)]
        [InlineData(0, 2, DeliveryStatus.Failed)]
        [InlineData(0, 0, DeliveryStatus.Failed)]
        public void Delivery_status_from_counts(int sent, int failed, int expected)
        {
            Assert.Equal(expected, DeliveryStatus.From(sent, failed));
        }
    }
}
