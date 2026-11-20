using System;
using System.Diagnostics;
using System.Threading;
using GHIElectronics.TinyCLR.Devices.Pwm;
using GHIElectronics.TinyCLR.Pins;

namespace PortalDesktop.Device {
    /// <summary>
    /// The FEZ Portal's buzzer (PWM controller 3, pin PB1, as in the Countdown sample). Plays a
    /// "beep beep beep beep" pattern on its own thread so ringing never holds up the screen.
    /// </summary>
    public class Buzzer {
        const int Frequency = 2000;            // Hz
        const int RingSeconds = 60;            // a ring stops by itself after this

        const int Idle = 0, Ringing = 1, Chirping = 2;

        readonly AutoResetEvent wake = new AutoResetEvent(false);
        volatile int mode;
        PwmController controller;
        PwmChannel channel;
        Thread thread;

        public void Ring(bool on) {
            if (!on) {
                this.mode = Idle;
                return;
            }
            this.Play(Ringing);
        }

        public void Chirp() => this.Play(Chirping);

        void Play(int what) {
            this.mode = what;
            if (this.thread == null) {
                this.thread = new Thread(this.Run);
                this.thread.Start();
            }
            this.wake.Set();
        }

        void Run() {
            while (true) {
                this.wake.WaitOne();
                var playing = this.mode;
                try {
                    if (this.channel == null) this.Open();
                    var until = DateTime.UtcNow.AddSeconds(RingSeconds);
                    while (this.mode != Idle && DateTime.UtcNow < until) {
                        for (var i = 0; i < 4 && this.mode != Idle; i++) {
                            this.channel.Start();
                            Thread.Sleep(90);
                            this.channel.Stop();
                            Thread.Sleep(70);
                        }
                        if (this.mode == Chirping) break;
                        for (var waited = 0; waited < 700 && this.mode != Idle; waited += 50) Thread.Sleep(50);
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine("Buzzer: " + ex.Message);
                }
                try {
                    if (this.channel != null) this.channel.Stop();
                }
                catch { }
                // Don't cancel a new request that came in just now.
                if (this.mode == playing) this.mode = Idle;
            }
        }

        void Open() {
            this.controller = PwmController.FromName(SC20260.Timer.Pwm.Controller3.Id);
            this.channel = this.controller.OpenChannel(SC20260.Timer.Pwm.Controller3.PB1);
            this.controller.SetDesiredFrequency(Frequency);
            this.channel.SetActiveDutyCyclePercentage(0.5);
        }
    }
}
