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

        private static bool SetPixel(Color[] pixels, Point pt, Color c)
        {
            bool bChanged = false;
            int index = (int)(pt.X + pt.Y);
            if (index < pixels.Length && pixels[index] != c)
            {
                pixels[index] = c;
                bChanged = true;
            }
            return bChanged;
        }

        private static bool SetPixel(Color[] pixels, int index, Color c)
        {
            bool bChanged = false;
            if (index < pixels.Length && pixels[index] != c)
            {
                pixels[index] = c;
                bChanged = true;
            }
            return bChanged;
        }

        public static bool FloodFill(Color[] pixels, int imgWidth, int imgHeight, Point ptStart, int pencilSize, Color targetColor, Color replacementColor)
        {
            int nChanged = 0;
            Point ptOffset = GetPencilOffset(pencilSize, ptStart);
            bool[,] shape = GetPencilShape(pencilSize);

            for (double j = ptOffset.Y; j < ptOffset.Y + pencilSize; j++)
            {
                for (double i = ptOffset.X; i < ptOffset.X + pencilSize; i++)
                {
                    Point pt = new Point(i, j);
                    if (0 <= i && i < imgWidth && 0 <= j && j < imgHeight && shape[(int)(i - ptOffset.X), (int)(j - ptOffset.Y)])
                    {
                        int index = (int)(i + j * imgWidth);
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
                                nChanged += SetPixel(pixels, w, replacementColor) ? 1 : 0;
                                if ((w.Y > 0) && ColorMatch(GetPixel(pixels, new Point(w.X, w.Y - imgWidth)), targetColor))
                                    q.Enqueue(new Point(w.X, w.Y - imgWidth));
                                if ((w.Y < imgHeight * imgWidth - 1) && ColorMatch(GetPixel(pixels, new Point(w.X, w.Y + imgWidth)), targetColor))
                                    q.Enqueue(new Point(w.X, w.Y + imgWidth));
                                w.X--;
                            }
                            while ((e.X <= imgWidth - 1) && ColorMatch(GetPixel(pixels, e), targetColor))
                            {
                                nChanged += SetPixel(pixels, e, replacementColor) ? 1 : 0;
                                if ((e.Y > 0) && ColorMatch(GetPixel(pixels, new Point(e.X, e.Y - imgWidth)), targetColor))
                                    q.Enqueue(new Point(e.X, e.Y - imgWidth));
                                if ((e.Y < imgHeight * imgWidth - 1) && ColorMatch(GetPixel(pixels, new Point(e.X, e.Y + imgWidth)), targetColor))
                                    q.Enqueue(new Point(e.X, e.Y + imgWidth));
                                e.X++;
                            }
                        }
                    }
                }
            }
            return nChanged > 0;
        }

        public static bool RectangleFill(Color[] pixels, int imgWidth, int imgHeight, Point ptStart, Point ptEnd, Color replacementColor)
        {
            int nChanged = 0;
            for (double j = ptStart.Y * imgWidth; j < ptEnd.Y * imgWidth; j += imgWidth) 
            {
                for (double i = ptStart.X; i < ptEnd.X; i++)
                {                    
                    nChanged += SetPixel(pixels, new Point(i, j), replacementColor) ? 1 : 0;
                }
            }
            return nChanged > 0;
        }

        public static bool PencilFill(Color[] pixels, int imgWidth, int imgHeight, Point ptStart, int pencilSize, Color replacementColor)
        {
            int nChanged = 0;
            Point pt = GetPencilOffset(pencilSize, ptStart);
            bool[,] shape = GetPencilShape(pencilSize);            

            for (double j = pt.Y; j < pt.Y + pencilSize; j++)
            {
                for (double i = pt.X; i < pt.X + pencilSize; i++)
                {
                    if (0 <= i && i < imgWidth && 0 <= j && j < imgHeight && shape[(int)(i - pt.X), (int)(j - pt.Y)])
                    {
                        int index = (int)(i + j * imgWidth);
                        nChanged += SetPixel(pixels, index, replacementColor) ? 1 : 0;
                    }
                }
            }

            return nChanged > 0;
        }

        public static Point GetPencilOffset(int pencilSize, Point pencilPoint)
        {
            Point pt = pencilPoint;
            switch (pencilSize)
            {
                case 2:
                case 3:
                    pt.X -= 1;
                    pt.Y -= 1;
                    break;
                case 4:
                case 5:
                    pt.X -= 2;
                    pt.Y -= 2;
                    break;
                case 6:
                case 7:
                    pt.X -= 3;
                    pt.Y -= 3;
                    break;
                case 8:
                case 9:
                    pt.X -= 4;
                    pt.Y -= 4;
                    break;
                case 10:
                case 11:
                    pt.X -= 5;
                    pt.Y -= 5;
                    break;
                case 12:
                    pt.X -= 6;
                    pt.Y -= 6;
                    break;
            }
            return pt;
        }

        public static bool[,] GetPencilShape(int pencilSize)
        {
            bool[,] shape = new bool[pencilSize, pencilSize];
            for (int i = 0; i < pencilSize * pencilSize; i++)
            {
                shape[i % pencilSize, i / pencilSize] = true;
            }

            switch (pencilSize)
            {
                case 4:
                case 5:
                case 6:
                    shape[0, 0] = shape[pencilSize - 1, 0] = shape[0, pencilSize - 1] = shape[pencilSize - 1, pencilSize - 1] = false;
                    break;
                case 7:
                case 8:
                case 9:
                    shape[0, 0] = shape[pencilSize - 1, 0] = shape[0, pencilSize - 1] = shape[pencilSize - 1, pencilSize - 1] =
                        shape[1, 0] = shape[pencilSize - 2, 0] = shape[0, 1] = shape[pencilSize - 1, 1] =
                        shape[0, pencilSize - 2] = shape[pencilSize - 1, pencilSize - 2] = shape[1, pencilSize - 1] = shape[pencilSize - 2, pencilSize - 1] = false;
                    break;
                case 10:
                    shape[0, 0] = shape[pencilSize - 1, 0] = shape[0, pencilSize - 1] = shape[pencilSize - 1, pencilSize - 1] =
                        shape[1, 0] = shape[2, 0] = shape[pencilSize - 3, 0] = shape[pencilSize - 2, 0] =
                        shape[0, 1] = shape[pencilSize - 1, 1] = shape[0, 2] = shape[pencilSize - 1, 2] =
                        shape[0, pencilSize - 3] = shape[pencilSize - 1, pencilSize - 3] = shape[0, pencilSize - 2] = shape[pencilSize - 1, pencilSize - 2] =
                        shape[1, pencilSize - 1] = shape[2, pencilSize - 1] = shape[pencilSize - 3, pencilSize - 1] = shape[pencilSize - 2, pencilSize - 1] = false;
                    break;
                case 11:
                    shape[0, 0] = shape[1, 0] = shape[2, 0] = shape[pencilSize - 3, 0] = shape[pencilSize - 2, 0] = shape[pencilSize - 1, 0] =
                        shape[0, 1] = shape[1, 1] = shape[pencilSize - 2, 1] = shape[pencilSize - 1, 1] =
                        shape[0, 2] = shape[pencilSize - 1, 2] =
                        shape[0, pencilSize - 3] = shape[pencilSize - 1, pencilSize - 3] =
                        shape[0, pencilSize - 2] = shape[1, pencilSize - 2] = shape[pencilSize - 2, pencilSize - 2] = shape[pencilSize - 1, pencilSize - 2] =
                        shape[0, pencilSize - 1] = shape[1, pencilSize - 1] = shape[2, pencilSize - 1] = shape[pencilSize - 3, pencilSize - 1] = shape[pencilSize - 2, pencilSize - 1] = shape[pencilSize - 1, pencilSize - 1] = false;
                    break;
                case 12:
                    shape[0, 0] = shape[1, 0] = shape[2, 0] = shape[3, 0] = shape[pencilSize - 4, 0] = shape[pencilSize - 3, 0] = shape[pencilSize - 2, 0] = shape[pencilSize - 1, 0] =
                        shape[0, 1] = shape[1, 1] = shape[pencilSize - 2, 1] = shape[pencilSize - 1, 1] =
                        shape[0, 2] = shape[pencilSize - 1, 2] =
                        shape[0, 3] = shape[pencilSize - 1, 3] =
                        shape[0, pencilSize - 4] = shape[pencilSize - 1, pencilSize - 4] =
                        shape[0, pencilSize - 3] = shape[pencilSize - 1, pencilSize - 3] =
                        shape[0, pencilSize - 2] = shape[1, pencilSize - 2] = shape[pencilSize - 2, pencilSize - 2] = shape[pencilSize - 1, pencilSize - 2] =
                        shape[0, pencilSize - 1] = shape[1, pencilSize - 1] = shape[2, pencilSize - 1] = shape[3, pencilSize - 1] = shape[pencilSize - 4, pencilSize - 1] = shape[pencilSize - 3, pencilSize - 1] = shape[pencilSize - 2, pencilSize - 1] = shape[pencilSize - 1, pencilSize - 1] = false;
                    break;
            }
            return shape;
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
