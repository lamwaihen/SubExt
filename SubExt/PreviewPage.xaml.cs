using Lumia.Imaging;
using Lumia.Imaging.Adjustments;
using Lumia.Imaging.Artistic;
using Lumia.Imaging.Transforms;
using MediaCaptureReader;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO;
using System.Threading.Tasks;
using SubExt.Model;
using SubExt.ViewModel;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SubExt
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PreviewPage : Page
    {
        private Payload p;
        private Point m_ptRegionStart;
        private Rect m_rectPreview;

        private SwapChainPanelRenderer m_renderer;
        private MediaReader m_mediaReader;
        private bool m_isRendering;
        private byte[] m_previousFrame;
        public PreviewPage()
        {
            this.InitializeComponent();
            m_isRendering = false;

            swapChainPanelTarget.Loaded += swapChainPanelTarget_Loaded;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            p = e.Parameter as Payload;
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            //p.Region = new Rect(Canvas.GetLeft(rectRegion), Canvas.GetTop(rectRegion), rectRegion.ActualWidth, rectRegion.ActualHeight);
        }

        private async void buttonProceed_Click(object sender, RoutedEventArgs e)
        {
            p.VideoFrames = new System.Collections.ObjectModel.ObservableCollection<VideoFrame>();
             // Start from beginning
            m_mediaReader.Seek(TimeSpan.FromMilliseconds(sldPreview.Value));

            StorageFile file = null;
            for (int i = 0; i < 600; i++)
            {
                // Read each frame
                using (MediaReaderReadResult mediaResult = await m_mediaReader.VideoStream.ReadAsync())
                {
                    if (mediaResult.EndOfStream || mediaResult.Error)
                        break;

                    using (MediaSample2D inputSample = (MediaSample2D)mediaResult.Sample)
                    {
                        if (inputSample == null)
                            continue;

                        using (MediaBuffer2D inputBuffer = inputSample.LockBuffer(BufferAccessMode.Read))
                        {
                            // Wrap MediaBuffer2D in Bitmap
                            Bitmap inputBitmap = new Bitmap(
                            new Size(inputSample.Width, inputSample.Height),
                            ColorMode.Yuv420Sp,
                            new uint[] { inputBuffer.Planes[0].Pitch, inputBuffer.Planes[1].Pitch },
                            new IBuffer[] { inputBuffer.Planes[0].Buffer, inputBuffer.Planes[1].Buffer }
                            );

                            // Apply effect
                            using (CropEffect cropEffect = new CropEffect(new BitmapImageSource(inputBitmap), p.SubtitleRect))
                            using (ContrastEffect contrastEffect = new ContrastEffect(cropEffect))
                            using (GrayscaleNegativeEffect grayscaleNegativeEffect = new GrayscaleNegativeEffect(contrastEffect))
                            using (StampEffect stampEffect = new StampEffect(grayscaleNegativeEffect, (int)sliderStampSmoothness.Value, sliderStampThreshold.Value))
                            {
                                // Render to a bitmap
                                WriteableBitmap WB = new WriteableBitmap((int)p.SubtitleRect.Width, (int)p.SubtitleRect.Height);
                                WriteableBitmapRenderer renderer = new WriteableBitmapRenderer(stampEffect, WB);
                                await renderer.RenderAsync();

                                byte[] pixels = PostProcessing(WB);
                                if (pixels != null)
                                {
                                    file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(((int)inputSample.Timestamp.TotalMilliseconds).ToString("D8") + ".jpg", CreationCollisionOption.ReplaceExisting);
                                    using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                                    {
                                        var propertySet = new BitmapPropertySet();
                                        propertySet.Add("ImageQuality", new BitmapTypedValue(1.0, PropertyType.Single));
                                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, propertySet);
                                        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                                            (uint)WB.PixelWidth, (uint)WB.PixelHeight,
                                            96.0, 96.0, pixels);
                                        await encoder.FlushAsync();

                                        VideoFrame frame = new VideoFrame()
                                        {
                                            BeginTime = inputSample.Timestamp,
                                            EndTime = inputSample.Timestamp,
                                            Image = file,
                                            ImageSize = new Size(WB.PixelWidth, WB.PixelHeight)   
                                        };
                                        p.VideoFrames.Add(frame);
                                    }
                                }
                                else
                                {
                                    string filename = file.Name;
                                    int pos = filename.LastIndexOf("-");
                                    if (pos > 0)
                                        filename = filename.Substring(0, pos + 1) + ((int)inputSample.Timestamp.TotalMilliseconds).ToString("D8") + ".jpg";
                                    else
                                    {
                                        string sub = filename.Substring(0, filename.LastIndexOf("."));
                                        filename = sub + "-" + ((int)inputSample.Timestamp.TotalMilliseconds).ToString("D8") + ".jpg";
                                    }
                                    await file.RenameAsync(filename);

                                    p.VideoFrames[p.VideoFrames.Count - 1].EndTime = inputSample.Timestamp;
                                }
                            }
                        }
                    }
                }
            }

            Frame.Navigate(typeof(SubtitlePage), p);
        }
        private void effects_Changed(object sender, RoutedEventArgs e)
        {
            SeekVideo(TimeSpan.FromMilliseconds(sldPreview.Value));

            CheckBox checkBox = sender as CheckBox;
            if (checkBox == checkBoxStamp)
            {
                sliderStampSmoothness.Visibility = checkBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                sliderStampThreshold.Visibility = checkBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        private void sldPreview_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            SeekVideo(TimeSpan.FromMilliseconds(e.NewValue));
        }
        private void sliderStamp_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            SeekVideo(TimeSpan.FromMilliseconds(sldPreview.Value));
        }
        private void swapChainPanelTarget_Loaded(object sender, RoutedEventArgs e)
        {
            if (swapChainPanelTarget.ActualHeight > 0 && swapChainPanelTarget.ActualWidth > 0)
            {
                if (m_renderer == null)
                {
                    OpenPreviewVideo();
                }
            }

            swapChainPanelTarget.SizeChanged += async (s, args) =>
            {
                OpenPreviewVideo();
            };
        }
        private void swapChainPanelTarget_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            rectRegion.Width = rectRegion.Height = 0;
            rectRegion.Visibility = Visibility.Collapsed;

            PointerPoint ptrPt = e.GetCurrentPoint(swapChainPanelTarget);
            m_ptRegionStart = ptrPt.Position;
            if (!m_rectPreview.Contains(m_ptRegionStart))
                return;

            // Initialize the rectangle.
            // Set border color and width
            rectRegion.Visibility = Visibility.Visible;

            Canvas.SetLeft(rectRegion, m_ptRegionStart.X);
            Canvas.SetTop(rectRegion, m_ptRegionStart.X);
        }
        private void swapChainPanelTarget_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint ptrPt = e.GetCurrentPoint(swapChainPanelTarget);
            if (!ptrPt.Properties.IsLeftButtonPressed || rectRegion.Visibility == Visibility.Collapsed)
                return;

            var pos = ptrPt.Position;

            // Set the position of rectangle
            var x = Math.Min(pos.X, m_ptRegionStart.X);
            var y = Math.Min(pos.Y, m_ptRegionStart.Y);

            // Set the dimenssion of the rectangle
            var w = Math.Max(pos.X, m_ptRegionStart.X) - x;
            var h = Math.Max(pos.Y, m_ptRegionStart.Y) - y;

            rectRegion.Width = w;
            rectRegion.Height = h;

            Canvas.SetLeft(rectRegion, x);
            Canvas.SetTop(rectRegion, y);
        }
        private void swapChainPanelTarget_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            m_ptRegionStart = new Point(0, 0);
            double w = p.OriginalSize.Width / m_rectPreview.Width;
            double h = p.OriginalSize.Height / m_rectPreview.Height;
            p.SubtitleRect = new Rect((Canvas.GetLeft(rectRegion) - m_rectPreview.Left) * w, (Canvas.GetTop(rectRegion) - m_rectPreview.Top) * h, rectRegion.ActualWidth * w, rectRegion.ActualHeight * h);
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            canvasPreview.Height = swapChainPanelTarget.Height = (canvasPreview.Parent as StackPanel).ActualHeight - sldPreview.ActualHeight;
            canvasPreview.Width = swapChainPanelTarget.Width = (canvasPreview.Parent as StackPanel).ActualWidth;
        }

        private async void CalculateMediaRect()
        {
            using (var mediaResult = await m_mediaReader.VideoStream.ReadAsync())
            {
                MediaSample2D inputSample = (MediaSample2D)mediaResult.Sample;

                double uar = swapChainPanelTarget.ActualWidth / swapChainPanelTarget.ActualHeight; // Aspect ratio of UI
                double par = (double)inputSample.Width / inputSample.Height;    // Aspect ratio of video

                if (par > uar)
                {
                    double height = (double)swapChainPanelTarget.ActualWidth / par;
                    m_rectPreview = new Rect(0, (double)(swapChainPanelTarget.ActualHeight - height) / 2, swapChainPanelTarget.ActualWidth, height);
                }
                else
                {
                    double width = (double)swapChainPanelTarget.ActualHeight * par;
                    m_rectPreview = new Rect((double)(swapChainPanelTarget.ActualWidth - width) / 2, 0, width, swapChainPanelTarget.ActualHeight);
                }
             }
        }
        private double CompareFrames(byte[] img1, byte[] img2)
        {
            if (img2 == null)
                return 0;

            byte[] diff = new byte[img1.Length / 4];
            for (int i = 0; i < img1.Length / 4; i++)
            {
                // since the pixels are either black or white, only check the first byte.
                diff[i] = (byte)(img1[i * 4] == img2[i * 4] ? 1 : 0);
            }

            double sameCount = 0;
            foreach (byte item in diff)
            {
                if (item == 1)
                    sameCount++;
            }
            return sameCount / (img1.Length / 4);
        }
        private void ConvertToBlackWhite(byte[] pixels, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // First turn all pixels to B/W
                    pixels[(x + y * width) * 4] = (byte)(pixels[(x + y * width) * 4] >= 128 ? 255 : 0);
                    pixels[(x + y * width) * 4 + 1] = (byte)(pixels[(x + y * width) * 4 + 1] >= 128 ? 255 : 0);
                    pixels[(x + y * width) * 4 + 2] = (byte)(pixels[(x + y * width) * 4 + 2] >= 128 ? 255 : 0);
                }
            }

            //for (int y = 0; y < height; y++)
            //{
            //    for (int x = 0; x < width; x++)
            //    {
            //        if (y == 0 || y == height - 1 || x == 0 || x == width - 1)
            //        {
            //            FloodFill(pixels, width, height, new Point(x, y), Color.FromArgb(255, 0, 0, 0), Color.FromArgb(255, 255, 255, 255));
            //        }
            //    }
            //}

            //for (int i = 0; i < pixels.Length; i++)
            //{
            //    if (pixels[i] != 255)
            //        return false;
            //}
            //return true;
        }

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

        static void FloodFill(byte[] pixels, int width, int height, Point pt, Color targetColor, Color replacementColor)
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
        private async void OpenPreviewVideo()
        {
            if (p.Video != null)
            {
                m_mediaReader = await MediaReader.CreateFromFileAsync(p.Video);
                sldPreview.Maximum = m_mediaReader.Duration.TotalMilliseconds;
                sldPreview.Value = 10000;
                CalculateMediaRect();
            }
        }
        private byte[] PostProcessing(WriteableBitmap source)
        {
            byte[] sourcePixels = source.PixelBuffer.ToArray();
            // Change to black and white first
            ConvertToBlackWhite(sourcePixels, source.PixelWidth, source.PixelHeight);

            // Floodfile to clear the edges
            for (int y = 0; y < source.PixelHeight; y++)
            {
                for (int x = 0; x < source.PixelWidth; x++)
                {
                    if (y == 0 || y == source.PixelHeight - 1 || x == 0 || x == source.PixelWidth - 1)
                    {
                        FloodFill(sourcePixels, source.PixelWidth, source.PixelHeight, new Point(x, y), Color.FromArgb(255, 0, 0, 0), Color.FromArgb(255, 255, 255, 255));
                    }
                }
            }

            // Resize 


            // Compare with previous frame
            if (CompareFrames(sourcePixels, m_previousFrame) > 0.98)
            {
                return null;
            }
            else
            {
                m_previousFrame = sourcePixels;
                return sourcePixels;
            }
        }
        private async void SeekVideo(TimeSpan position)
        {
            if (m_mediaReader == null)
                return;

            if (!m_isRendering)
            {
                m_isRendering = true;
                m_mediaReader.Seek(position);
                // Get the specific frame and show on screen
                using (MediaReaderReadResult mediaResult = await m_mediaReader.VideoStream.ReadAsync())
                {
                    MediaSample2D inputSample = (MediaSample2D)mediaResult.Sample;
                    if (p.OriginalSize.Width == 0)
                        p.OriginalSize = new Size(inputSample.Width, inputSample.Height);

                    using (MediaBuffer2D inputBuffer = inputSample.LockBuffer(BufferAccessMode.Read))
                    {
                        // Wrap MediaBuffer2D in Bitmap
                        var inputBitmap = new Bitmap(
                            new Size(inputSample.Width, inputSample.Height),
                            ColorMode.Yuv420Sp,
                            new uint[] { inputBuffer.Planes[0].Pitch, inputBuffer.Planes[1].Pitch },
                            new IBuffer[] { inputBuffer.Planes[0].Buffer, inputBuffer.Planes[1].Buffer }
                            );

                        // Apply effect
                        using (ContrastEffect contrastEffect = new ContrastEffect())
                        using (GrayscaleNegativeEffect grayscaleNegativeEffect = new GrayscaleNegativeEffect())
                        using (StampEffect stampEffect = new StampEffect((int)sliderStampSmoothness.Value, sliderStampThreshold.Value))
                        {
                            EffectList appliedEffects = new EffectList();
                            if (checkBoxContrast.IsChecked == true)
                                appliedEffects.Add(contrastEffect);
                            if (checkBoxStamp.IsChecked == true)
                                appliedEffects.Add(stampEffect);
                            if (checkBoxGrayscaleNegative.IsChecked == true)
                                appliedEffects.Add(grayscaleNegativeEffect);

                            // Apply image to the first effect
                            if (appliedEffects.Count > 0)
                                ((IImageConsumer)appliedEffects[0]).Source = new BitmapImageSource(inputBitmap);
                            else
                                appliedEffects.Source = new BitmapImageSource(inputBitmap);

                            m_renderer = new SwapChainPanelRenderer(appliedEffects, swapChainPanelTarget);

                            await m_renderer.RenderAsync();
                            m_isRendering = false;
                        }
                    }
                }
            }
        }
    }
}
