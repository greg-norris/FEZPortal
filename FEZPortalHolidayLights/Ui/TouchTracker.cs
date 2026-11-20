using System.Collections;

namespace HolidayLights.Ui {
    /// <summary>
    /// Turns the touch driver's events into a clean queue of Down / Move / Up for the UI thread.
    ///
    /// The FT5xx6 driver doesn't queue anything itself: on each interrupt it reads the panel's state at
    /// that moment. When its handler runs late (the CPU was busy), the "press down" state is often
    /// already gone and the first thing reported is a move or even the lift. So the finger state is
    /// tracked here: a move or lift with no finger down starts a touch at that point, and every touch
    /// gets exactly one Down and one Up. Moves are merged: only the latest position waits in the queue.
    /// Safe to call from the driver's thread while the UI thread calls <see cref="Next"/>.
    /// </summary>
    public class TouchTracker {
        public const int Down = 0, Move = 1, Up = 2;

        readonly ArrayList queue = new ArrayList();
        bool down;
        int lastX, lastY;

        public void Pressed(int x, int y) {
            lock (this.queue) {
                if (this.down) this.Add(Up, this.lastX, this.lastY);   // the lift was missed
                this.down = true;
                this.Add(Down, x, y);
            }
        }

        public void Moved(int x, int y) {
            lock (this.queue) {
                var kind = this.down ? Move : Down;   // the press was missed: start here
                this.down = true;
                this.Add(kind, x, y);
            }
        }

        /// <param name="known">False when the panel gave no position with the lift.</param>
        public void Lifted(int x, int y, bool known) {
            lock (this.queue) {
                if (!this.down) {
                    if (!known) return;
                    this.Add(Down, x, y);   // a quick tap seen only as it ended
                }
                this.down = false;
                if (known) this.Add(Up, x, y);
                else this.Add(Up, this.lastX, this.lastY);
            }
        }

        /// <summary>The next event as { kind, x, y }, or null.</summary>
        public int[] Next() {
            lock (this.queue) {
                if (this.queue.Count == 0) return null;
                var e = (int[])this.queue[0];
                this.queue.RemoveAt(0);
                return e;
            }
        }

        /// <summary>Call inside lock(queue).</summary>
        void Add(int kind, int x, int y) {
            this.lastX = x;
            this.lastY = y;
            var n = this.queue.Count;
            if (kind == Move && n > 0 && ((int[])this.queue[n - 1])[0] == Move) {
                this.queue[n - 1] = new[] { kind, x, y };
                return;
            }
            this.queue.Add(new[] { kind, x, y });
        }
    }
}
