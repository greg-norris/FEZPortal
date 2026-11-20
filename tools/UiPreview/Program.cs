using System;
using System.Drawing.Imaging;
using FEZPortalHelloWorld.Ui;

namespace UiPreview {
    /// <summary>Renders the dashboard to a PNG (plus a 2x zoomed copy) without needing the board.</summary>
    static class Program {
        static int Main(string[] args) {
            if (args.Length != 2) {
                Console.Error.WriteLine("usage: UiPreview <font dir> <out.png>");
                return 1;
            }
            var canvas = new Canvas(480, 272, args[0]);
            new Dashboard().Draw(canvas);
            canvas.Bitmap.Save(args[1], ImageFormat.Png);

            using (var zoom = new System.Drawing.Bitmap(960, 544))
            using (var g = System.Drawing.Graphics.FromImage(zoom)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(canvas.Bitmap, 0, 0, 960, 544);
                zoom.Save(System.IO.Path.ChangeExtension(args[1], ".2x.png"), ImageFormat.Png);
            }
            Console.WriteLine("Wrote " + args[1]);
            return 0;
        }
    }
}
