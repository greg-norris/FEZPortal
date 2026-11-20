using System;
using PortalDesktop.Data;
using PortalDesktop.Ui;

namespace PortalDesktop.App {
    /// <summary>
    /// One full-screen page below the status bar. The shell redraws the whole page when
    /// <see cref="Stamp"/> changes, and calls <see cref="Tick"/> once a second for small updates.
    /// All methods run on the UI thread with the state locked.
    /// </summary>
    public abstract class Screen {
        protected readonly Shell Shell;

        protected Screen(Shell shell) {
            this.Shell = shell;
        }

        protected PortalState State => this.Shell.State;

        /// <summary>Shown in the status bar next to the back arrow.</summary>
        public abstract string Title { get; }

        /// <summary>Extra status bar text for this screen (e.g. "Market open"), or null.</summary>
        public virtual string BarText => null;

        /// <summary>When true, a coloured dot is drawn in front of <see cref="BarText"/>.</summary>
        public virtual bool HasBarDot => false;

        public virtual System.Drawing.Color BarDotColor => Theme.TextDim;

        /// <summary>
        /// A button in the status bar, just left of the time: "+" draws a yellow plus, any other text a
        /// labelled button. Null for none. Taps on it go to <see cref="BarButtonTap"/>.
        /// </summary>
        public virtual string BarButton => null;

        public virtual void BarButtonTap() { }

        /// <summary>Changes whenever data this screen shows changes.</summary>
        public virtual int Stamp => 0;

        /// <summary>Called when the screen is opened.</summary>
        public virtual void Opened() { }

        /// <summary>Draws everything below the status bar.</summary>
        public abstract void Draw(Canvas c);

        /// <summary>Once a second: redraw (and flush) whatever shows the time.</summary>
        public virtual void Tick(Canvas c, DateTime now, DateTime previous) { }

        /// <summary>
        /// Called on every pass of the UI loop (~30 ms) for screens with fast-changing parts, such as
        /// stopwatch tenths. Redraw and flush only what changed.
        /// </summary>
        public virtual void Frame(Canvas c) { }

        /// <summary>True for pages that use the whole display, without the status bar.</summary>
        public virtual bool FullScreen => false;

        /// <summary>Where the back arrow leads (null = the desktop).</summary>
        public virtual Screen BackTo => null;

        public virtual void Tap(int x, int y) { }

        /// <summary>direction: +1 = swipe left (next), -1 = swipe right (previous).</summary>
        public virtual void Swipe(int direction) { }

        /// <summary>direction: +1 = swipe up (show what's below), -1 = swipe down (show what's above).</summary>
        public virtual void VerticalSwipe(int direction) { }

        /// <summary>The back arrow was tapped. Return true when the screen handled it itself.</summary>
        public virtual bool Back() => false;

        protected static bool Inside(int x, int y, int left, int top, int width, int height) =>
            x >= left && x < left + width && y >= top && y < top + height;

        /// <summary>Background card used by every screen.</summary>
        protected static void Card(Canvas c, int x, int y, int w, int h) => c.FillRoundRect(Theme.Panel, x, y, w, h, Theme.Radius);
    }
}
