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
using Windows.System.Threading;
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
        private Point m_ptOffset;
        private FrameworkElement m_pressedDot;

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

            PropertySet previewEffectPropertySet = new PropertySet();
            previewEffectPropertySet["SubtitleRect"] = p.SubtitleRect;
            previewEffectPropertySet["VideoFilename"] = p.Name;
            previewEffectPropertySet["OnFrameProceeded"] = (Action<TimeSpan>)OnFrameProceeded;
            MediaClip clip = await MediaClip.CreateFromFileAsync(p.Video);
            clip.VideoEffectDefinitions.Add(new VideoEffectDefinition(typeof(ExtractVideoEffect).FullName, previewEffectPropertySet));
            VideoEncodingProperties videoProps = clip.GetVideoEncodingProperties();
            MediaComposition composition = new MediaComposition();
            composition.Clips.Add(clip);

            mediaProceed.SetMediaStreamSource(composition.GenerateMediaStreamSource());
            mediaProceed.Play();            
        }
        private void buttonRegion_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            switch (element.Name)
            {
                case "buttonRegionXDec": p.SubtitleRect = new Rect(p.SubtitleRect.X - 1, p.SubtitleRect.Y, p.SubtitleRect.Width, p.SubtitleRect.Height); break;
                case "buttonRegionXInc": p.SubtitleRect = new Rect(p.SubtitleRect.X + 1, p.SubtitleRect.Y, p.SubtitleRect.Width, p.SubtitleRect.Height); break;
                case "buttonRegionYDec": p.SubtitleRect = new Rect(p.SubtitleRect.X, p.SubtitleRect.Y - 1, p.SubtitleRect.Width, p.SubtitleRect.Height); break;
                case "buttonRegionYInc": p.SubtitleRect = new Rect(p.SubtitleRect.X, p.SubtitleRect.Y + 1, p.SubtitleRect.Width, p.SubtitleRect.Height); break;
                case "buttonRegionWDec": p.SubtitleRect = new Rect(p.SubtitleRect.X, p.SubtitleRect.Y, p.SubtitleRect.Width - 1, p.SubtitleRect.Height); break;
                case "buttonRegionWInc": p.SubtitleRect = new Rect(p.SubtitleRect.X, p.SubtitleRect.Y, p.SubtitleRect.Width + 1, p.SubtitleRect.Height); break;
                case "buttonRegionHDec": p.SubtitleRect = new Rect(p.SubtitleRect.X, p.SubtitleRect.Y, p.SubtitleRect.Width, p.SubtitleRect.Height - 1); break;
                case "buttonRegionHInc": p.SubtitleRect = new Rect(p.SubtitleRect.X, p.SubtitleRect.Y, p.SubtitleRect.Width, p.SubtitleRect.Height + 1); break;
                default:
                    break;
            }
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
        private void rectRegion_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint ptrPt = e.GetCurrentPoint(mediaFrame);
            if (!ptrPt.Properties.IsLeftButtonPressed || rectRegion.Visibility == Visibility.Collapsed || m_pressedDot == null)
                return;

            var pos = ptrPt.Position;

            Rect rectNew = p.SubtitleUIRect;
            Debug.WriteLine(string.Format("Point {0}\tstart {1}\tRect {2}", pos.ToString(), m_ptRegionStart.ToString(), rectNew.ToString()));
            switch ((sender as FrameworkElement).Name)
            {
                case "dotRegionLT":
                    rectNew.Width = Math.Max(5, rectNew.Right - pos.X);
                    rectNew.Height = Math.Max(5, rectNew.Bottom - pos.Y);
                    rectNew.X = pos.X;
                    rectNew.Y = pos.Y;
                    break;
                case "dotRegionRT":
                    rectNew.Width = Math.Max(5, pos.X - rectNew.Left);
                    rectNew.Height = Math.Max(5, rectNew.Bottom - pos.Y);
                    rectNew.Y = pos.Y;
                    break;
                case "dotRegionLB":
                    rectNew.Width = Math.Max(5, rectNew.Right - pos.X);
                    rectNew.Height = Math.Max(5, pos.Y - rectNew.Top);
                    rectNew.X = pos.X;
                    break;
                case "dotRegionRB":
                    rectNew.Width = Math.Max(5, pos.X - p.SubtitleUIRect.X);
                    rectNew.Height = Math.Max(5, pos.Y - p.SubtitleUIRect.Y);
                    break;
                case "rectRegion":
                    rectNew.X += (pos.X - m_ptRegionStart.X);
                    rectNew.Y += (pos.Y - m_ptRegionStart.Y);
                    m_ptRegionStart = pos;
                    break;
            }

            p.SubtitleUIRect = rectNew;
            e.Handled = true;
        }
        private void rectRegion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint ptrPt = e.GetCurrentPoint(mediaFrame);
            if (ptrPt.Properties.IsLeftButtonPressed)
            {
                m_pressedDot = sender as FrameworkElement;
                m_pressedDot.CapturePointer(e.Pointer);
                m_ptRegionStart = ptrPt.Position;
                m_ptOffset = new Point(m_ptRegionStart.X - p.SubtitleUIRect.X, m_ptRegionStart.Y - p.SubtitleUIRect.Y);

                e.Handled = true;
            }
        }
        private void rectRegion_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            m_ptRegionStart = m_ptOffset = new Point(0, 0);
            if (m_pressedDot != null)
            {
                m_pressedDot.ReleasePointerCapture(e.Pointer);
                m_pressedDot = null;
            }
            e.Handled = true;
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
            //SeekVideo(TimeSpan.FromMilliseconds(sliderPreview.Value));
        }
        private void swapChainPanelTarget_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            //rectRegion.Width = rectRegion.Height = 0;
            //rectRegion.Visibility = Visibility.Collapsed;

            PointerPoint ptrPt = e.GetCurrentPoint(mediaFrame);
            if (!p.VideoPreview.Contains(ptrPt.Position))
                return;

            //m_ptRegionStart = ptrPt.Position;

            // Initialize the rectangle.
            // Set border color and width
            if (rectRegion.Visibility == Visibility.Collapsed)
            {
                rectRegion.Visibility = Visibility.Visible;

                p.SubtitleUIRect = new Rect(ptrPt.Position.X - (p.SubtitleUIRect.Width / 2), ptrPt.Position.Y - (p.SubtitleUIRect.Height / 2), p.SubtitleUIRect.Width, p.SubtitleUIRect.Height);
            }
            e.Handled = true;
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
        /// <summary>
        /// Avoid thread deadlock when set property from callback function.
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="time"></param>
        private void DelayTimerElpasedHandler(ThreadPoolTimer timer, TimeSpan time)
        {
            p.CurrentFrameTime = time;
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

                            //m_renderer = new SwapChainPanelRenderer(appliedEffects, swapChainPanelTarget);

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
                StorageFolder folder = await ApplicationData.Current.TemporaryFolder.GetFolderAsync(p.Name);

                IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
                string[] separators = new string[] { "-", ".bmp" };
                foreach (StorageFile file in files)
                {
                    if (!char.IsDigit(file.Name, 0))
                        continue;

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
                // Create XML project file
                p.ProjectFile = await folder.CreateFileAsync(p.DisplayName + ".xml", CreationCollisionOption.ReplaceExisting);
                await G.SaveXml(p.ProjectFile, p.VideoFrames);

                Frame.Navigate(typeof(SubtitlePage), p);
            }
        }

        public async void OnFrameProceeded(TimeSpan time)
        {
            Debug.WriteLine("Proceeded {0} of {1}", time.TotalMilliseconds, p.Duration.TotalMilliseconds);

            // Use a timer to update property to avoid thread deadlock.
            ThreadPoolTimer timerPlay = ThreadPoolTimer.CreateTimer((sender) => DelayTimerElpasedHandler(sender, time), TimeSpan.FromMilliseconds(10));

            TimeSpan ts = time.Add(TimeSpan.FromMilliseconds((double)p.FrameRate.Denominator / p.FrameRate.Numerator * 1000));
            if (false && ts >= p.Duration)
            {
                StorageFolder folder = await ApplicationData.Current.TemporaryFolder.GetFolderAsync(p.Name);
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
                }
                G.UIThreadExecute(() =>
                {
                    Frame.Navigate(typeof(SubtitlePage), p);
                });
            }
        }
    }

    public class TimeSpanToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return 0;

            TimeSpan ts = (TimeSpan)value;
            return ts.TotalMilliseconds;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
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
                switch (param)
                {
                    case "X":
                        return _rt.X;
                    case "Y":
                        return _rt.Y;
                    case "W":
                        return _rt.Width;
                    case "H":
                        return _rt.Height;
                    case "LTX":
                        return _rt.Left - 4;
                    case "LTY":
                        return _rt.Top - 4;
                    case "RTX":
                        return _rt.Right - 4;
                    case "RTY":
                        return _rt.Top - 4;
                    case "LBX":
                        return _rt.Left - 4;
                    case "LBY":
                        return _rt.Bottom - 4;
                    case "RBX":
                        return _rt.Right - 4;
                    case "RBY":
                        return _rt.Bottom - 4;
                }
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
