using System.Collections.Generic;

namespace SmartWatering.Plugins.Logic
{
    /// <summary>One Switch Command row, reduced to what the state calculation needs.</summary>
    public struct SwitchReading
    {
        /// <summary>Creates a reading.</summary>
        /// <param name="choiceValue">Value of the Switch choice column, or null when the row has none.</param>
        /// <param name="isOn">Value of the Status column (true = ON).</param>
        public SwitchReading(int? choiceValue, bool isOn)
        {
            ChoiceValue = choiceValue;
            IsOn = isOn;
        }

        /// <summary>Value of the Switch choice column, or null when the row has none.</summary>
        public int? ChoiceValue { get; }

        /// <summary>Value of the Status column (true = ON).</summary>
        public bool IsOn { get; }
    }

    /// <summary>
    /// The one answer the device needs: four booleans, true = relay ON.
    /// Built from Switch Command rows sorted newest first; the newest row per switch wins.
    /// </summary>
    public sealed class SwitchStates
    {
        /// <summary>Motor (main pump) wanted ON.</summary>
        public bool Motor { get; private set; }

        /// <summary>Ground floor solenoid wanted ON.</summary>
        public bool GroundFloor { get; private set; }

        /// <summary>First floor solenoid wanted ON.</summary>
        public bool FirstFloor { get; private set; }

        /// <summary>Second floor solenoid wanted ON.</summary>
        public bool SecondFloor { get; private set; }

        /// <summary>
        /// Takes rows sorted newest first and keeps the first row seen for each switch.
        /// A switch with no row stays false (OFF), and rows without a known switch are ignored.
        /// </summary>
        /// <param name="newestFirst">Switch Command rows, newest first.</param>
        public static SwitchStates FromNewestFirst(IEnumerable<SwitchReading> newestFirst)
        {
            var states = new SwitchStates();
            var seen = new HashSet<int>();
            if (newestFirst == null) return states;

            foreach (var row in newestFirst)
            {
                if (!row.ChoiceValue.HasValue || !Switches.IsKnown(row.ChoiceValue.Value)) continue;
                int choice = row.ChoiceValue.Value;
                if (!seen.Add(choice)) continue;   // an older row for a switch we already have

                switch (choice)
                {
                    case Switches.Motor: states.Motor = row.IsOn; break;
                    case Switches.GroundFloor: states.GroundFloor = row.IsOn; break;
                    case Switches.FirstFloor: states.FirstFloor = row.IsOn; break;
                    case Switches.SecondFloor: states.SecondFloor = row.IsOn; break;
                }

                if (seen.Count == 4) break;
            }

            return states;
        }

        /// <summary>The same JSON the web API returns to the ESP32.</summary>
        public string ToJson()
        {
            return "{\"motor\":" + B(Motor) + ",\"groundFloor\":" + B(GroundFloor) +
                   ",\"firstFloor\":" + B(FirstFloor) + ",\"secondFloor\":" + B(SecondFloor) + "}";
        }

        private static string B(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
