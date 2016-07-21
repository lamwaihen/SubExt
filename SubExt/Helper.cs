using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using System.Numerics;
namespace SubExt
{
    public class Helper
    {
        private static bool ColorMatch(Color a, Color b)
        {
            return a.Equals(b);
        }
        private static Color GetPixel(Color[] pixels, Point pt)
        {
            return pixels[(int)(pt.X + pt.Y)];
            //int current = (int)(x + y * width);
            //pixels[current * 4] = pixels[current * 4] >= 48 ? byte.MaxValue : byte.MinValue;
            //pixels[current * 4 + 1] = pixels[current * 4 + 1] >= 48 ? byte.MaxValue : byte.MinValue;
            //pixels[current * 4 + 2] = pixels[current * 4 + 2] >= 48 ? byte.MaxValue : byte.MinValue;
            //pixels[current * 4 + 3] = byte.MaxValue;

            //return Color.FromArgb(pixels[current * 4 + 3], pixels[current * 4 + 2], pixels[current * 4 + 1], pixels[current * 4]);
        }

        private static void SetPixel(Color[] pixels, Point pt, Color c)
        {
            pixels[(int)(pt.X + pt.Y)] = c;
            //pixels[(int)(x + y * width) * 4] = c.B;
            //pixels[(int)(x + y * width) * 4 + 1] = c.G;
            //pixels[(int)(x + y * width) * 4 + 2] = c.R;
            //pixels[(int)(x + y * width) * 4 + 3] = c.A;
        }

        public static void FloodFill(Color[] pixels, int imgWidth, int imgHeight, Point pt, Color targetColor, Color replacementColor)
        {
            Queue<Point> q = new Queue<Point>();
            pt.Y *= imgWidth;
            q.Enqueue(pt);
            while (q.Count > 0)
            {
                Point n = q.Dequeue();
                if (!ColorMatch(GetPixel(pixels, n), targetColor))
                    continue;
                Point w = n, e = new Point(n.X + 1, n.Y);
                while ((w.X >= 0) && ColorMatch(GetPixel(pixels, w), targetColor))
                {
                    SetPixel(pixels, w, replacementColor);
                    if ((w.Y > 0) && ColorMatch(GetPixel(pixels, new Point(w.X, w.Y - imgWidth)), targetColor))
                        q.Enqueue(new Point(w.X, w.Y - imgWidth));
                    if ((w.Y < imgHeight * imgWidth - 1) && ColorMatch(GetPixel(pixels, new Point(w.X, w.Y + imgWidth)), targetColor))
                        q.Enqueue(new Point(w.X, w.Y + imgWidth));
                    w.X--;
                }
                while ((e.X <= imgWidth - 1) && ColorMatch(GetPixel(pixels, e), targetColor))
                {
                    SetPixel(pixels,e, replacementColor);
                    if ((e.Y > 0) && ColorMatch(GetPixel(pixels, new Point(e.X, e.Y - imgWidth)), targetColor))
                        q.Enqueue(new Point(e.X, e.Y - imgWidth));
                    if ((e.Y < imgHeight * imgWidth - 1) && ColorMatch(GetPixel(pixels, new Point(e.X, e.Y + imgWidth)), targetColor))
                        q.Enqueue(new Point(e.X, e.Y + imgWidth));
                    e.X++;
                }
            }
        }

        public static void RectangleFill(Color[] pixels, int imgWidth, int imgHeight, Point ptStart, Point ptEnd, Color replacementColor)
        {
            for (double j = ptStart.Y * imgWidth; j < ptEnd.Y * imgWidth; j += imgWidth) 
            {
                for (double i = ptStart.X; i < ptEnd.X; i++)
                {
                    SetPixel(pixels, new Point(i, j), replacementColor);
                }
            }
        }
    }

    public static class DispatcherHelper
    {
        public static CoreDispatcher UIDispatcher { get; private set; }

        static public void UIThreadExecute(Action action)
        {
            try
            {
                InnerExecute(action).Wait();
            }
            catch (AggregateException e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
        }
        static private async Task InnerExecute(Action action)
        {
            if (UIDispatcher.HasThreadAccess)
                action();
            else
                await UIDispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
        }
        static DispatcherHelper()
        {
            if (UIDispatcher != null)
                return;
            else
                UIDispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
        }
    }

}
