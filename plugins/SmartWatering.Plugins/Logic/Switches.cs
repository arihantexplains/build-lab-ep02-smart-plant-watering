namespace SmartWatering.Plugins.Logic
{
    /// <summary>
    /// The four switches of the watering system, as values of the Switch choice column
    /// on the Switch Command table. Pure logic: no Dataverse types, so the tests can link this file.
    /// </summary>
    public static class Switches
    {
        /// <summary>Motor, the main pump that pulls water from the tank.</summary>
        public const int Motor = 100000000;

        /// <summary>Ground floor solenoid (tap that feeds the ground floor drip line).</summary>
        public const int GroundFloor = 100000001;

        /// <summary>First floor solenoid.</summary>
        public const int FirstFloor = 100000002;

        /// <summary>Second floor solenoid.</summary>
        public const int SecondFloor = 100000003;

        /// <summary>Receiver text that means "send to every number in the admin list".</summary>
        public const string AllAdmins = "All admins";

        /// <summary>True when the value is one of the four switches.</summary>
        /// <param name="choiceValue">Value of the Switch choice column.</param>
        public static bool IsKnown(int choiceValue)
        {
            return choiceValue >= Motor && choiceValue <= SecondFloor;
        }

        /// <summary>The label of a switch as it appears in the app and the choice column.</summary>
        /// <param name="choiceValue">Value of the Switch choice column.</param>
        /// <returns>The label, or null for an unknown value.</returns>
        public static string Label(int choiceValue)
        {
            switch (choiceValue)
            {
                case Motor: return "Motor";
                case GroundFloor: return "Ground floor solenoid";
                case FirstFloor: return "First floor solenoid";
                case SecondFloor: return "Second floor solenoid";
                default: return null;
            }
        }

        /// <summary>
        /// The WhatsApp alert text sent when a switch turns ON:
        /// "Motor started" for the motor, "Watering Ground floor" (and so on) for a floor.
        /// </summary>
        /// <param name="choiceValue">Value of the Switch choice column.</param>
        /// <returns>The alert text, or null for an unknown value.</returns>
        public static string AlertText(int choiceValue)
        {
            switch (choiceValue)
            {
                case Motor: return "Motor started";
                case GroundFloor: return "Watering Ground floor";
                case FirstFloor: return "Watering First floor";
                case SecondFloor: return "Watering Second floor";
                default: return null;
            }
        }
    }
}
