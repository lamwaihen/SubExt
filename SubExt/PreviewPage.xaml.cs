using Lumia.Imaging;
using Lumia.Imaging.Adjustments;
using MediaCaptureReader;
using System;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

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
        private Point m_ptRegionMoveStart;
        private Rect m_rectMedia;

        private GrayscaleEffect m_grayscaleEffect;
        private SwapChainPanelRenderer m_renderer;
        private MediaReader m_mediaReader;
        private bool m_isRendering;
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
        }
        
        private void sldPreview_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            SeekVideo(TimeSpan.FromMilliseconds(e.NewValue));
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
            if (!m_rectMedia.Contains(m_ptRegionStart))
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
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            canvasPreview.Height = swapChainPanelTarget.Height = this.ActualHeight - sldPreview.ActualHeight;
            canvasPreview.Width = swapChainPanelTarget.Width = this.ActualWidth;
        }

        private async void CalculateMediaRect()
        {
            using (var mediaResult = await m_mediaReader.VideoStream.ReadAsync())
            {
                MediaSample2D inputSample = (MediaSample2D)mediaResult.Sample;
                m_rectMedia.Width = swapChainPanelTarget.ActualWidth;
                m_rectMedia.Height = (double)inputSample.Height / inputSample.Width * m_rectMedia.Width;
                m_rectMedia.X = 0;
                m_rectMedia.Y = (double)(swapChainPanelTarget.ActualHeight - m_rectMedia.Height) / 2;
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
        private async void SeekVideo(TimeSpan position)
        {
            if (!m_isRendering)
            {
                m_isRendering = true;
                m_mediaReader.Seek(position);
                // Get the specific frame and show on screen
                using (var mediaResult = await m_mediaReader.VideoStream.ReadAsync())
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
                        GrayscaleEffect _grayscaleEffect = new GrayscaleEffect();
                        ((IImageConsumer)_grayscaleEffect).Source = new Lumia.Imaging.BitmapImageSource(inputBitmap);
                        m_renderer = new SwapChainPanelRenderer(_grayscaleEffect, swapChainPanelTarget);
                        await m_renderer.RenderAsync();
                        m_isRendering = false;
                    }
                }
            }
        }
    }
}
