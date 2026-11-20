using System;

namespace HolidayLights.Lights {
    /// <summary>
    /// Works out each frame for the strip from the settings: runs the effect, applies brightness and
    /// the power limit, and leaves the SPI bytes in <see cref="Buffer"/>. It has no hardware of its own
    /// (the device thread sends the buffer), so the desktop preview can run it too.
    /// The LED count can change while it runs (Setup page): the arrays have room for
    /// LightSettings.MaxCount LEDs. After the count goes down, the LEDs past the new end keep being
    /// sent as off, so a strip that is still connected there goes dark instead of freezing.
    /// </summary>
    public class LightEngine {
        readonly LightSettings settings;
        readonly Effects effects;
        readonly int[] frame;
        readonly int[] shown;           // copy of the last frame for the screen, under lock(this)
        DateTime last = DateTime.MinValue;
        double phase;
        int rateSpeed = -1;
        double rate;
        int stillVersion = -1;          // settings version of the still frame now in Buffer (-1 = none)
        int shownCount;

        int count;                      // LEDs the effects light
        int sendCount;                  // LEDs sent: the most the count has been since start-up

        /// <summary>The bytes to send to the strip after each <see cref="Step"/> (the first <see cref="SendLength"/> of them).</summary>
        public readonly byte[] Buffer;

        /// <summary>LEDs on the strip, as set in the settings.</summary>
        public int Count => this.count;

        /// <summary>How many bytes of <see cref="Buffer"/> to send.</summary>
        public int SendLength => StripOutput.BufferSize(this.sendCount);

        /// <summary>Set by the Setup page (inside lock(settings)): show the red/green/blue test pattern.</summary>
        public bool TestPattern;

        /// <summary>Estimated current of the last frame, and what it would have been without the limit.</summary>
        public int Milliamps, WantedMilliamps;

        public LightEngine(LightSettings settings) {
            this.settings = settings;
            this.count = this.sendCount = settings.Count;
            const int capacity = LightSettings.MaxCount;
            this.effects = new Effects(capacity);
            this.frame = new int[capacity];
            this.shown = new int[capacity];
            this.Buffer = new byte[StripOutput.BufferSize(capacity)];
        }

        /// <summary>How much faster than normal the animation runs at a speed setting: 0.25x at 0, 4x at 100.</summary>
        public static double Rate(int speed) {
            var rate = 1.0;
            var steps = speed - 50;
            // 2^(steps / 25), without Math.Pow.
            var f = steps >= 0 ? 1.0281138266560665 : 1 / 1.0281138266560665;   // 2^(1/25)
            if (steps < 0) steps = -steps;
            for (var i = 0; i < steps; i++) rate *= f;
            return rate;
        }

        /// <summary>
        /// Works out the frame for the given moment. Returns false when nothing changed: Solid, Off and
        /// the test pattern are only worked out again after a settings change.
        /// </summary>
        public bool Step(DateTime utcNow) {
            bool on, test;
            int effect, rgb, brightness, speed, order, limit, version, count;
            lock (this.settings) {
                var s = this.settings;
                on = s.On;
                test = this.TestPattern;
                effect = s.Effect;
                rgb = s.Rgb;
                brightness = s.Brightness;
                speed = s.Speed;
                order = s.Order;
                limit = s.PowerLimit;
                count = s.Count;
                version = s.Version + (test ? 1 << 30 : 0);
            }

            // Seconds since the last frame (none for the first, and no big jump after a pause or clock change).
            var dt = this.last == DateTime.MinValue ? 0 : (utcNow - this.last).Ticks / (double)TimeSpan.TicksPerSecond;
            if (dt < 0 || dt > 0.2) dt = 0.2;
            this.last = utcNow;
            if (speed != this.rateSpeed) {
                this.rate = Rate(speed);
                this.rateSpeed = speed;
            }
            dt *= this.rate;
            this.phase += dt;
            if (this.phase > 1e7) this.phase = 0;

            if (count != this.count) {
                // LEDs past a shorter strip are sent as off from now on.
                for (var i = count; i < this.count; i++) this.frame[i] = 0;
                this.count = count;
                if (count > this.sendCount) this.sendCount = count;
            }

            var still = test || !on || Effects.IsStill(effect);
            if (still && version == this.stillVersion) return false;
            this.stillVersion = still ? version : -1;

            if (test) {
                Effects.TestPattern(this.count, this.frame);
                brightness = 40;
            }
            else if (on) {
                this.effects.Render(effect, rgb, this.count, this.phase, dt, this.frame);
            }
            else {
                for (var i = 0; i < this.count; i++) this.frame[i] = 0;
            }

            int wanted;
            var milliamps = StripOutput.Encode(this.frame, this.sendCount, brightness, order, limit, this.Buffer, out wanted);
            lock (this) {
                Array.Copy(this.frame, this.shown, this.count);
                this.shownCount = this.count;
                this.Milliamps = milliamps;
                this.WantedMilliamps = wanted;
            }
            return true;
        }

        /// <summary>Copies the last frame (full-strength colours) for drawing on the screen; returns its LED count.</summary>
        public int CopyShown(int[] target) {
            lock (this) {
                Array.Copy(this.shown, target, this.shownCount);
                return this.shownCount;
            }
        }
    }
}
