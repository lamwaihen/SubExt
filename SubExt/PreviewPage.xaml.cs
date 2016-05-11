﻿using Lumia.Imaging;
using Lumia.Imaging.Adjustments;
using Lumia.Imaging.Artistic;
using Lumia.Imaging.Transforms;
using MediaCaptureReader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Runtime.InteropServices.WindowsRuntime;
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
        public Payload p = new Payload();
        private Point m_ptRegionStart;

        private SwapChainPanelRenderer m_renderer;
        private MediaReader m_mediaReader;
        private bool m_isRendering;
        private byte[] m_previousFrame;
        public PreviewPage()
        {
            InitializeComponent();
            m_isRendering = false;

            swapChainPanelTarget.Loaded += swapChainPanelTarget_Loaded;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            p = e.Parameter as Payload;
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
        }

        private async void buttonProceed_Click(object sender, RoutedEventArgs e)
        {
            p.VideoFrames = new System.Collections.ObjectModel.ObservableCollection<VideoFrame>();
             // Start from beginning
            m_mediaReader.Seek(TimeSpan.FromMilliseconds(0));
            // Total frames
            Windows.Media.MediaProperties.VideoEncodingProperties v = m_mediaReader.VideoStream.GetCurrentStreamProperties();
            int frames = (int)(((double)v.FrameRate.Numerator / v.FrameRate.Denominator) * m_mediaReader.Duration.TotalSeconds);

            progressPostProcessing.Maximum = frames;
            StorageFile file = null;
            for (int i = 0; i < frames; i++)
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
                                    file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(((int)inputSample.Timestamp.TotalMilliseconds).ToString("D8") + ".bmp", CreationCollisionOption.ReplaceExisting);
                                    using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                                    {
                                        var propertySet = new BitmapPropertySet();
                                        propertySet.Add("ImageQuality", new BitmapTypedValue(1.0, PropertyType.Single));
                                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
                                        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                                            (uint)WB.PixelWidth, (uint)WB.PixelHeight,
                                            96.0, 96.0, pixels);
                                        await encoder.FlushAsync();

                                        VideoFrame frame = new VideoFrame()
                                        {
                                            BeginTime = inputSample.Timestamp,
                                            EndTime = inputSample.Timestamp,
                                            ImageFile = file,
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
                                        filename = filename.Substring(0, pos + 1) + ((int)inputSample.Timestamp.TotalMilliseconds).ToString("D8") + ".bmp";
                                    else
                                    {
                                        string sub = filename.Substring(0, filename.LastIndexOf("."));
                                        filename = sub + "-" + ((int)inputSample.Timestamp.TotalMilliseconds).ToString("D8") + ".jpg";
                                    }
                                    await file.RenameAsync(filename, NameCollisionOption.ReplaceExisting);

                                    p.VideoFrames[p.VideoFrames.Count - 1].EndTime = inputSample.Timestamp;
                                }
                                Debug.WriteLine(string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss.fff"), file.Name));
                            }
                        }
                    }
                }
                progressPostProcessing.Value++;
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
            if (!p.VideoPreview.Contains(m_ptRegionStart))
                return;

            // Initialize the rectangle.
            // Set border color and width
            rectRegion.Visibility = Visibility.Visible;

            p.SubtitleUIRect = new Rect(m_ptRegionStart.X, m_ptRegionStart.Y, 0, 0);
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

            p.SubtitleUIRect = new Rect(x, y, w, h);
        }
        private void swapChainPanelTarget_PointerReleased(object sender, PointerRoutedEventArgs e)
        {            
            m_ptRegionStart = new Point(0, 0);
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
                if (p.VideoSize.Width == 0)
                    p.VideoSize = new Size(inputSample.Width, inputSample.Height);

                double uar = swapChainPanelTarget.ActualWidth / swapChainPanelTarget.ActualHeight; // Aspect ratio of UI
                double par = (double)inputSample.Width / inputSample.Height;    // Aspect ratio of video

                if (par > uar)
                {
                    double height = swapChainPanelTarget.ActualWidth / par;
                    p.VideoPreview = new Rect(0, (swapChainPanelTarget.ActualHeight - height) / 2, swapChainPanelTarget.ActualWidth, height);
                }
                else
                {
                    double width = swapChainPanelTarget.ActualHeight * par;
                    p.VideoPreview = new Rect((swapChainPanelTarget.ActualWidth - width) / 2, 0, width, swapChainPanelTarget.ActualHeight);
                }

                // Try to read from settings
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(p.VideoSize.ToString()))
                    p.SubtitleRect = (Rect)ApplicationData.Current.LocalSettings.Values[p.VideoSize.ToString()];
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
        }
        private async void OpenPreviewVideo()
        {
            if (p?.Video != null)
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
                        Helper.FloodFill(sourcePixels, source.PixelWidth, source.PixelHeight, new Point(x, y), Colors.Black, Colors.White);
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

    public class SubtitleRectFormatter : IValueConverter
    {
        private Rect _rt;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null)
            {                
                _rt = (Rect)value;
                string p = parameter as string;
                if (p == "X")
                    return _rt.X.ToString("0.00");
                else if (p == "Y")
                    return _rt.Y.ToString("0.00");
                else if (p == "W")
                    return _rt.Width.ToString("0.00");
                else if (p == "H")
                    return _rt.Height.ToString("0.00");
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            string p = parameter as string;
            if (p == "X")
                _rt.X = double.Parse(value as string);
            else if (p == "Y")
                _rt.Y = double.Parse(value as string);
            else if (p == "W")
                _rt.Width = double.Parse(value as string);
            else if (p == "H")
                _rt.Height = double.Parse(value as string);

            return _rt;
        }
    }

    public class SubtitleUIRectFormatter : IValueConverter
    { 
        private Rect _rt;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null)
            {
                _rt = (Rect)value;
                string param = parameter as string;
                if (param == "X")
                    return _rt.X;
                else if (param == "Y")
                    return _rt.Y;
                else if (param == "W")
                    return _rt.Width;
                else if (param == "H")
                    return _rt.Height;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
