# Holiday Lights

Touch-screen control of an LPD8806 RGB LED strip (GE32RGB8806A, 160 LEDs) from the FEZ Portal.

## Wiring

| Strip | Portal                    |
|-------|---------------------------|
| CI (clock) | PI1 / SPI2 SCK       |
| DI (data)  | PI3 / SPI2 MOSI      |
| 5V    | 5 V supply (see Power)    |
| GND   | GND                       |

Connect the strip's input end (arrows on the strip point away from it). SPI runs at 2 MHz
(`LedStrip.ClockHz`).

## Power

Each LED draws up to 60 mA at full white, so 160 LEDs can need 9.6 A at 5 V. That is far more than the
Portal's 5 V pin or a USB port can supply. The program estimates the strip's current every frame and dims
the whole strip to stay under the **power limit** (1 A to start; change it on the Setup page). The status
bar shows the estimate, in yellow when the limit is dimming the lights.

For a bright strip, power it from its own 5 V supply rated above the limit, and connect that supply's
ground to the Portal's GND as well. Leave only clock, data and ground going to the Portal.

## First run

Open **Setup**: the first 10 LEDs should be red, the next 10 green and the next 10 blue, and the **last
10 white**. If the colours are in a different order, tap the order buttons until they match. Most LPD8806
strips are GRB.

## More than one strip

Join the next strip's input (CI, DI, 5V, GND) to the end of the previous one (CO, DO, 5V, GND), then set
**LEDs** on the Setup page (-32 / +32 is one metre): 320 for two 160-LED strips, up to 512. The white
test LEDs should then sit at the very end of the last strip; if they show up earlier, the count is too
small, and if they don't light at all, the data isn't getting through the joint (or the count is too big).

A strip's thin copper can't carry much current along 10 m: feed 5 V and GND into the joint between the
strips as well (from the same supply), or the far end goes dim and orange at high brightness.

## Using it

- Effects: Solid, Rainbow, Chase, Twinkle, Breathe, Comet and Classic (old-style bulb colours).
- **Holidays** (the last effect button) opens a page of holidays; tap one for its light themes. The
  Holidays button, the holiday and the theme that is on show in yellow. Each holiday page has its own
  Brightness and Speed sliders; the arrow at the top left goes back.

  | Holiday | Themes (`effect=` name in lights.txt) |
  |---------|---------------------------------------|
  | New Year's | Fireworks (`newyear-fireworks`), Champagne (`newyear-champagne`), Gold & silver (`newyear-goldsilver`), Midnight (`newyear-midnight`) |
  | Valentine's Day | Heartbeat (`valentines-heartbeat`), Sweethearts (`valentines-sweethearts`), Love sparkle (`valentines-sparkle`), Pink & red (`valentines-pinkred`) |
  | St. Patrick's Day | Shamrock (`stpatricks-shamrock`), Pot of gold (`stpatricks-potofgold`), Irish flag (`stpatricks-flag`), Clover drift (`stpatricks-drift`) |
  | Easter | Pastels (`easter-pastels`), Easter eggs (`easter-eggs`), Spring sparkle (`easter-sparkle`) |
  | Fourth of July | Fireworks (`july4-fireworks`), Flag (`july4-flag`), Sparklers (`july4-sparklers`), Parade (`july4-parade`) |
  | Halloween | Pumpkin (`halloween-pumpkin`), Spooky (`halloween-spooky`), Lightning (`halloween-lightning`), Candy corn (`halloween-candycorn`) |
  | Thanksgiving | Autumn leaves (`thanksgiving-leaves`), Harvest (`thanksgiving-harvest`), Candlelight (`thanksgiving-candles`) |
  | Hanukkah | Blue & white (`hanukkah-bluewhite`), Candles (`hanukkah-candles`), Starlight (`hanukkah-starlight`), Festival (`hanukkah-festival`) |
  | Christmas | Candy cane (`christmas-candycane`), Red & green (`christmas-redgreen`), Snowfall (`christmas-snowfall`), Christmas tree (`christmas-tree`) |

  Themes are listed in `Lights\Holidays.cs`: each is a pattern (stripes, bulbs, sparkle, fade, drift,
  fireworks, heartbeat, flicker, lightning or wave) with its own colours, so a new one is one line there.
  An old lights.txt with `effect=candycane` opens as Christmas / Candy cane.
- The colour bar (tap or drag) and the Warm / White swatches set the colour for Solid, Chase, Twinkle,
  Breathe and Comet. Picking a colour while another effect or a holiday theme runs switches to Solid.
- Brightness and Speed sliders follow your finger. ON / OFF is at the top right.
- Settings are saved to `HolidayLights\lights.txt` on the SD card a couple of seconds after a change, and
  come back at power-up. The file can be edited on a PC (`count=` changes the number of LEDs).

## Preview on the PC

`powershell -NoProfile -ExecutionPolicy Bypass -File FEZPortalHolidayLights\Preview\preview.ps1`

Renders every page to `Preview\bin\*.png` and checks the bytes sent to the strip, the power limit and
settings saving.
