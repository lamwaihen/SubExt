using Lumia.Imaging;
using Lumia.Imaging.Adjustments;
using Lumia.Imaging.Artistic;
using Lumia.Imaging.Transforms;
using MediaCaptureReader;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
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
using Windows.Media.Effects;
using VideoEffects;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SubExt
{
    public class PreviewUIState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public bool? IsCheckBoxStampChecked
        {
            get { return _isCheckBoxStampChecked; }
            set { _isCheckBoxStampChecked = value.GetValueOrDefault(false); RaisePropertyChanged(); }
        }
        private bool _isCheckBoxStampChecked = false;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PreviewPage : Page
    {
        static private CoreDispatcher UIdispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
        static public IAsyncAction UIThreadAsync(DispatchedHandler function)
        {
            return UIdispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, function);
        }

        public PreviewUIState u = new PreviewUIState();
        public Payload p = new Payload();
        private Point m_ptRegionStart;

        private SwapChainPanelRenderer m_renderer;
        private MediaReader m_mediaReader;
        private bool m_isRendering;
        private byte[] m_previousFrame;
        private CanvasBitmap m_bitmapFrame;
        private IPropertySet m_previewEffectPropertySet;
        public PreviewPage()
        {
            InitializeComponent();
            m_isRendering = false;

            //swapChainPanelTarget.Loaded += swapChainPanelTarget_Loaded;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            p = e.Parameter as Payload;
            OpenPreviewVideo();
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
        }
        void Page_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private async void buttonProceed_Click(object sender, RoutedEventArgs e)
        {
            p.VideoFrames = new System.Collections.ObjectModel.ObservableCollection<VideoFrame>();

             var previewEffectPropertySet = new PropertySet();
            previewEffectPropertySet["SubtitleRect"] = p.SubtitleRect;
            previewEffectPropertySet["VideoFilename"] = p.Video.Name;
            previewEffectPropertySet["OnFrameProceeded"] = (Action<TimeSpan>)OnFrameProceeded;
            MediaClip clip = await MediaClip.CreateFromFileAsync(p.Video);
            clip.VideoEffectDefinitions.Add(new VideoEffectDefinition(typeof(ExtractVideoEffect).FullName, previewEffectPropertySet));
            VideoEncodingProperties videoProps = clip.GetVideoEncodingProperties();
            MediaComposition composition = new MediaComposition();
            composition.Clips.Add(clip);
          
            mediaProceed.SetMediaStreamSource(composition.GenerateMediaStreamSource());
            mediaProceed.Play();
        }
        private void effects_Changed(object sender, RoutedEventArgs e)
        {
            //SeekVideo(TimeSpan.FromMilliseconds(sliderPreview.Value));

            CheckBox checkBox = sender as CheckBox;
            if (checkBox == checkBoxStamp)
            {
                sliderStampSmoothness.Visibility = checkBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                sliderStampThreshold.Visibility = checkBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        private void sliderPreview_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            //SeekVideo(TimeSpan.FromMilliseconds(e.NewValue));

            // Overloaded constructor takes the arguments days, hours, minutes, seconds, miniseconds.
            // Create a TimeSpan with miliseconds equal to the slider value.
            TimeSpan ts = new TimeSpan(0, 0, 0, 0, (int)sliderPreview.Value);
            mediaFrame.Position = ts;
        }
        private void sliderStamp_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            SeekVideo(TimeSpan.FromMilliseconds(sliderPreview.Value));
        }
        private void swapChainPanelTarget_Loaded(object sender, RoutedEventArgs e)
        {
            return;
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
            //canvasPreview.Height = mediaFrame.Height = (canvasPreview.Parent as FrameworkElement).ActualHeight - sliderPreview.ActualHeight;
            //canvasPreview.Width = mediaFrame.Width = (canvasPreview.Parent as FrameworkElement).ActualWidth;
        }

        private void CalculateMediaRect()
        {
            if (p.VideoSize.Width == 0)
                p.VideoSize = new Size(mediaFrame.NaturalVideoWidth, mediaFrame.NaturalVideoHeight);

            double uar = mediaFrame.Width / mediaFrame.Height; // Aspect ratio of UI
            double par = (double)mediaFrame.NaturalVideoWidth / mediaFrame.NaturalVideoHeight;    // Aspect ratio of video

            if (par > uar)
            {
                double height = mediaFrame.Width / par;
                p.VideoPreview = new Rect(0, (mediaFrame.Height - height) / 2, mediaFrame.Width, height);
            }
            else
            {
                double width = mediaFrame.Height * par;
                p.VideoPreview = new Rect((mediaFrame.Width - width) / 2, 0, width, mediaFrame.Height);
            }

            // Try to read from settings
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey(p.VideoSize.ToString()))
                p.SubtitleRect = (Rect)ApplicationData.Current.LocalSettings.Values[p.VideoSize.ToString()];
        }
        private async void OpenPreviewVideo()
        {
            if (p?.Video != null)
            {
                try
                {
                    m_previewEffectPropertySet = new PropertySet();

                    MediaClip clip = await MediaClip.CreateFromFileAsync(p.Video);
                    clip.VideoEffectDefinitions.Add(new VideoEffectDefinition(typeof(PreviewVideoEffect).FullName, m_previewEffectPropertySet));
                    VideoEncodingProperties videoProps = clip.GetVideoEncodingProperties();
                    p.FrameRate = videoProps.FrameRate;
                    p.Duration = clip.OriginalDuration;

                    progressPostProcessing.Maximum = p.Duration.TotalSeconds / ((double)p.FrameRate.Denominator / p.FrameRate.Numerator);

                    MediaComposition composition = new MediaComposition();
                    composition.Clips.Add(clip);
                    mediaFrame.SetMediaStreamSource(composition.GenerateMediaStreamSource());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
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

                        // Win2D
                        if (m_bitmapFrame != null)
                            m_bitmapFrame.Dispose();
                        
                        byte[] b = inputBuffer.Planes[0].Buffer.ToArray();
                        //m_bitmapFrame = CanvasBitmap.CreateFromBytes(CanvasDevice.GetSharedDevice(), b, inputSample.Width, inputSample.Height, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    }
                }
            }
        }
        
        private void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            mediaFrame.Play();
        }

        private void buttonPause_Click(object sender, RoutedEventArgs e)
        {
            mediaFrame.Pause();
        }

        private void mediaFrame_MediaOpened(object sender, RoutedEventArgs e)
        {
            sliderPreview.Maximum = mediaFrame.NaturalDuration.TimeSpan.TotalMilliseconds;
            mediaFrame.Width = canvasPreview.ActualWidth;
            mediaFrame.Height = canvasPreview.ActualHeight;

            sliderPreview.Value = 10000;
            CalculateMediaRect();
        }

        private void M_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox t = sender as TextBox;
            m_previewEffectPropertySet[t.Name] = Convert.ToInt16(t.Text);
        }

        private async void mediaProceed_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            if (mediaProceed.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Paused)
            {
                StorageFolder folder = await ApplicationData.Current.TemporaryFolder.GetFolderAsync(p.Video.Name);
                IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
                string[] separators = new string[] { "-", ".bmp" };
                foreach (StorageFile file in files)
                {
                    string[] timestamps = file.Name.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                    VideoFrame frame = new VideoFrame()
                    {
                        BeginTime = TimeSpan.FromMilliseconds(Convert.ToDouble(timestamps[0])),
                        ImageFile = file
                    };
                    if (timestamps.Length == 1)
                        frame.EndTime = TimeSpan.FromMilliseconds(Convert.ToDouble(timestamps[0]));
                    else
                        frame.EndTime = TimeSpan.FromMilliseconds(Convert.ToDouble(timestamps[1]));

                    Windows.Storage.FileProperties.ImageProperties imgProps = await file.Properties.GetImagePropertiesAsync();
                    frame.ImageSize = new Size(imgProps.Width, imgProps.Height);
                    p.VideoFrames.Add(frame);
                    progressPostProcessing.Value++;
                }

                Frame.Navigate(typeof(SubtitlePage), p);
            }
        }

        public void OnFrameProceeded(TimeSpan time)
        {
            UIThreadAsync(async () =>
            {
                progressPostProcessing.Value++;

                if (progressPostProcessing.Value == progressPostProcessing.Maximum)
                {
                    StorageFolder folder = await ApplicationData.Current.TemporaryFolder.GetFolderAsync(p.Video.Name);
                    IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
                    string[] separators = new string[] { "-", ".bmp" };
                    foreach (StorageFile file in files)
                    {
                        string[] timestamps = file.Name.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                        VideoFrame frame = new VideoFrame()
                        {
                            BeginTime = TimeSpan.FromMilliseconds(Convert.ToDouble(timestamps[0])),
                            ImageFile = file
                        };
                        if (timestamps.Length == 1)
                            frame.EndTime = TimeSpan.FromMilliseconds(Convert.ToDouble(timestamps[0]));
                        else
                            frame.EndTime = TimeSpan.FromMilliseconds(Convert.ToDouble(timestamps[1]));

                        Windows.Storage.FileProperties.ImageProperties imgProps = await file.Properties.GetImagePropertiesAsync();
                        frame.ImageSize = new Size(imgProps.Width, imgProps.Height);
                        p.VideoFrames.Add(frame);
                        progressPostProcessing.Value++;
                    }

                    Frame.Navigate(typeof(SubtitlePage), p);
                }
            });
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
