# Flow: Smart Watering, scheduled run

Waters the three floors one after the other, three times a day. It never switches a relay itself: it adds rows to the Switch Command table, the web API turns the newest rows into four booleans, and the ESP32 follows within about 1.5 seconds.

**Create it:** make.powerapps.com > your solution > New > Automation > Cloud flow > Scheduled. Name: `Smart Watering, scheduled run`.

## Trigger

| Setting | Value |
|---|---|
| Trigger | Recurrence |
| Interval | 1 |
| Frequency | Day |
| Time zone | **Your time zone**, for example `(UTC+05:30) Chennai, Kolkata, Mumbai, New Delhi` |
| At these hours | `6, 12, 16` |
| At these minutes | `0` |

Set the time zone. Without it the trigger runs on UTC, so "6 AM" in India would really be 11:30 AM.

## Steps

Every "Add a new row" step uses the Dataverse connector, table **Switch Commands**. Set Name to something readable (it is only for the history), Switch to the choice shown, Status to **Yes** for ON or **No** for OFF, and Source to `Schedule`.

Put steps 1 to 15 inside a **Scope** called `Water the floors`.

| # | Action | Switch | Status | Why |
|---|---|---|---|---|
| 1 | Add a new row | Ground floor solenoid | Yes (ON) | Tap first. |
| 2 | Add a new row | Motor | Yes (ON) | The board starts the motor only after a tap has been open 2 seconds. |
| 3 | Delay, 2 minutes | | | Ground floor waters. |
| 4 | Add a new row | First floor solenoid | Yes (ON) | Hand-over: the next tap opens **before** the previous one closes. |
| 5 | Delay, 10 seconds | | | Gives the board time to see the new tap open (1.5 s poll + 2 s valve lead), so the motor never runs against closed taps and does not stop in between. |
| 6 | Add a new row | Ground floor solenoid | No (OFF) | Previous tap closes. |
| 7 | Delay, 2 minutes | | | First floor waters. |
| 8 | Add a new row | Second floor solenoid | Yes (ON) | Hand-over again. |
| 9 | Delay, 10 seconds | | | Same reason as step 5. |
| 10 | Add a new row | First floor solenoid | No (OFF) | |
| 11 | Delay, 2 minutes | | | Second floor waters. |
| 12 | Add a new row | Motor | No (OFF) | Motor stops first. |
| 13 | Delay, 5 seconds | | | Pressure drops before the last tap closes (the board also waits 2 seconds on its own). |
| 14 | Add a new row | Second floor solenoid | No (OFF) | Everything is off. |
| 15 | Add a new row (table **Send WhatsApp Messages**), optional | | | Message text `Watering done, motor stopped`, From `Smart watering`, Receiver `All admins`, Send **Yes**. |

The ON rows in steps 1, 2, 4 and 8 also fire the alert plugin, so the admins get "Watering Ground floor", "Motor started", "Watering First floor" and "Watering Second floor" without any extra flow step.

**Mind the 10 minute limit.** The board switches anything off that has been ON for 10 minutes. With 2 minutes per floor the motor runs about 6 minutes 20 seconds. If you lengthen the floors, keep the motor's total under 10 minutes, or raise `HARD_MAX_MINUTES` in `arduino/CallAPI/config.h` on purpose.

## Error handling

Add a second **Scope** after the first, called `On failure`. Open its **Settings** > **Run after** and tick only **has failed** and **has timed out** for `Water the floors`.

| # | Action | Details |
|---|---|---|
| 1 | Add a new row, Switch Commands | Motor, Status No (OFF). Motor first. |
| 2 | Add a new row, Switch Commands | Ground floor solenoid, No (OFF) |
| 3 | Add a new row, Switch Commands | First floor solenoid, No (OFF) |
| 4 | Add a new row, Switch Commands | Second floor solenoid, No (OFF) |
| 5 | Add a new row, Send WhatsApp Messages | Message text `Watering schedule failed, all switches set OFF. Check the flow run history.`, Receiver `All admins`, Send Yes |
| 6 | Terminate | Status **Failed**, so the run shows red in the history. |

Even if this scope cannot run (for example Dataverse is down), the board is still safe: the motor stops on its own after 10 minutes, and after 10 failed polls everything goes OFF.

## Test it

1. Temporarily change the delays to 20 seconds and run the flow with **Test** > Manually.
2. Watch the Switch Command table and the ESP32 Serial Monitor: `[RELAY] Ground floor solenoid -> ON`, then `[RELAY] Motor -> ON` two seconds later, and so on.
3. Put the delays back to 2 minutes and save.
