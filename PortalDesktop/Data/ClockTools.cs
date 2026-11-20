using System;

namespace PortalDesktop.Data {
    /// <summary>
    /// The countdown on the Countdown page. Its length is a setting (Config.CountdownSeconds);
    /// this only tracks a run. Times are UTC, so time zone changes don't move the end.
    /// </summary>
    public class CountdownTimer {
        public bool Running;
        DateTime endUtc;
        long pausedMs = -1;     // time left when paused, -1 when not started

        /// <summary>Started and not reset (running or paused).</summary>
        public bool Active => this.Running || this.pausedMs >= 0;

        public bool Paused => !this.Running && this.pausedMs >= 0;

        public long RemainingMs(DateTime utc, int lengthSeconds) {
            if (this.Running) {
                var left = (this.endUtc - utc).Ticks / TimeSpan.TicksPerMillisecond;
                return left > 0 ? left : 0;
            }
            return this.pausedMs >= 0 ? this.pausedMs : lengthSeconds * 1000L;
        }

        public DateTime EndUtc => this.endUtc;

        public void Start(DateTime utc, int lengthSeconds) {
            var left = this.pausedMs >= 0 ? this.pausedMs : lengthSeconds * 1000L;
            if (left <= 0) return;
            this.endUtc = utc.AddMilliseconds(left);
            this.Running = true;
        }

        public void Pause(DateTime utc) {
            if (!this.Running) return;
            this.pausedMs = this.RemainingMs(utc, 0);
            this.Running = false;
        }

        public void Reset() {
            this.Running = false;
            this.pausedMs = -1;
        }

        /// <summary>True once, when a running countdown reaches zero (it is then reset).</summary>
        public bool CheckDone(DateTime utc) {
            if (!this.Running || utc < this.endUtc) return false;
            this.Reset();
            return true;
        }
    }

    /// <summary>The stopwatch on the Timer page, with laps.</summary>
    public class LapTimer {
        public const int MaxLaps = 99;

        public bool Running;
        DateTime startUtc;
        long storedMs;                          // time from earlier runs (before the last Stop)
        public readonly long[] Laps = new long[MaxLaps];   // total time at each lap
        public int LapCount;

        public long ElapsedMs(DateTime utc) {
            var ms = this.storedMs;
            if (this.Running) {
                var run = (utc - this.startUtc).Ticks / TimeSpan.TicksPerMillisecond;
                if (run > 0) ms += run;
            }
            return ms;
        }

        public void Start(DateTime utc) {
            if (this.Running) return;
            this.startUtc = utc;
            this.Running = true;
        }

        public void Stop(DateTime utc) {
            if (!this.Running) return;
            this.storedMs = this.ElapsedMs(utc);
            this.Running = false;
        }

        public void Lap(DateTime utc) {
            if (!this.Running || this.LapCount >= MaxLaps) return;
            this.Laps[this.LapCount++] = this.ElapsedMs(utc);
        }

        public void Reset() {
            this.Running = false;
            this.storedMs = 0;
            this.LapCount = 0;
        }

        /// <summary>Length of lap i (0 = first).</summary>
        public long LapLength(int i) => this.Laps[i] - (i > 0 ? this.Laps[i - 1] : 0);
    }

    /// <summary>The alarm on the Alarm page (local time). Saved in clock.txt.</summary>
    public class AlarmClock {
        public const int EveryDay = 0, Weekdays = 1, Once = 2;
        public const int SnoozeMinutes = 9;

        public int Hour = 7, Minute;
        public bool Enabled;
        public int Repeat = EveryDay;
        public DateTime SnoozeUntil = DateTime.MinValue;   // local time; MinValue = not snoozed
        DateTime lastRang = DateTime.MinValue;             // local minute it last rang

        /// <summary>On, or snoozed: what the bell icons show.</summary>
        public bool IsSet => this.Enabled || this.SnoozeUntil != DateTime.MinValue;

        public static string RepeatName(int repeat) => repeat == Weekdays ? "Weekdays" : repeat == Once ? "Once" : "Every day";

        /// <summary>
        /// Call every second with the local time: true when the alarm (or a snooze) should ring now.
        /// settingsChanged is true when a one-time alarm switched itself off (save clock.txt).
        /// </summary>
        public bool Check(DateTime local, out bool settingsChanged) {
            settingsChanged = false;
            if (this.SnoozeUntil != DateTime.MinValue && local >= this.SnoozeUntil) {
                this.SnoozeUntil = DateTime.MinValue;
                return true;
            }
            if (!this.Enabled || local.Hour != this.Hour || local.Minute != this.Minute) return false;
            if (this.Repeat == Weekdays && !IsWeekday(local)) return false;

            var minute = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0);
            if (minute == this.lastRang) return false;
            this.lastRang = minute;
            if (this.Repeat == Once) {
                this.Enabled = false;
                settingsChanged = true;
            }
            return true;
        }

        public void Snooze(DateTime local) => this.SnoozeUntil = local.AddMinutes(SnoozeMinutes);

        /// <summary>When it rings next (local), or MinValue when it is off.</summary>
        public DateTime Next(DateTime local) {
            if (this.SnoozeUntil != DateTime.MinValue) return this.SnoozeUntil;
            if (!this.Enabled) return DateTime.MinValue;
            var day = new DateTime(local.Year, local.Month, local.Day, this.Hour, this.Minute, 0);
            if (day <= local) day = day.AddDays(1);
            while (this.Repeat == Weekdays && !IsWeekday(day)) day = day.AddDays(1);
            return day;
        }

        static bool IsWeekday(DateTime t) => t.DayOfWeek != DayOfWeek.Saturday && t.DayOfWeek != DayOfWeek.Sunday;
    }
}
