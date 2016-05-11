using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace SubExt
{
    public class Helper
    {
        private static bool ColorMatch(Color a, Color b)
        {
            return a.Equals(b);
        }
        private static Color GetPixel(byte[] pixels, int width, double x, double y)
        {
            byte a, r, g, b;
            b = pixels[(int)(x + y * width) * 4];
            g = pixels[(int)(x + y * width) * 4 + 1];
            r = pixels[(int)(x + y * width) * 4 + 2];
            a = pixels[(int)(x + y * width) * 4 + 3];
            return Color.FromArgb(a, r, g, b);
        }

        private static void SetPixel(byte[] pixels, int width, double x, double y, Color c)
        {
            pixels[(int)(x + y * width) * 4] = c.B;
            pixels[(int)(x + y * width) * 4 + 1] = c.G;
            pixels[(int)(x + y * width) * 4 + 2] = c.R;
            pixels[(int)(x + y * width) * 4 + 3] = c.A;
        }

        public static void FloodFill(byte[] pixels, int width, int height, Point pt, Color targetColor, Color replacementColor)
        {
            Queue<Point> q = new Queue<Point>();
            q.Enqueue(pt);
            while (q.Count > 0)
            {
                Point n = q.Dequeue();
                if (!ColorMatch(GetPixel(pixels, width, n.X, n.Y), targetColor))
                    continue;
                Point w = n, e = new Point(n.X + 1, n.Y);
                while ((w.X >= 0) && ColorMatch(GetPixel(pixels, width, w.X, w.Y), targetColor))
                {
                    SetPixel(pixels, width, w.X, w.Y, replacementColor);
                    if ((w.Y > 0) && ColorMatch(GetPixel(pixels, width, w.X, w.Y - 1), targetColor))
                        q.Enqueue(new Point(w.X, w.Y - 1));
                    if ((w.Y < height - 1) && ColorMatch(GetPixel(pixels, width, w.X, w.Y + 1), targetColor))
                        q.Enqueue(new Point(w.X, w.Y + 1));
                    w.X--;
                }
                while ((e.X <= width - 1) && ColorMatch(GetPixel(pixels, width, e.X, e.Y), targetColor))
                {
                    SetPixel(pixels, width, e.X, e.Y, replacementColor);
                    if ((e.Y > 0) && ColorMatch(GetPixel(pixels, width, e.X, e.Y - 1), targetColor))
                        q.Enqueue(new Point(e.X, e.Y - 1));
                    if ((e.Y < height - 1) && ColorMatch(GetPixel(pixels, width, e.X, e.Y + 1), targetColor))
                        q.Enqueue(new Point(e.X, e.Y + 1));
                    e.X++;
                }
            }
        }
    }
}
