using MediaCaptureReader;
using MediaCaptureReaderExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Lumia.Imaging;
using Lumia.Imaging.Adjustments;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SubExt
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private StorageFile m_video;
        private Grid[] m_arrayGrids;

        private GrayscaleEffect m_grayscaleEffect;
        private SwapChainPanelRenderer m_renderer;
        private MediaReader m_mediaReader;
        public MainPage()
        {
            this.InitializeComponent();

            m_arrayGrids = new Grid[2];
            m_arrayGrids[0] = gridMain;
            m_arrayGrids[1] = gridRegionSelect;
            swapChainPanelTarget.Loaded += swapChainPanelTarget_Loaded;

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

        private async void buttonOpenVideo_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add("*");
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            m_video = await openPicker.PickSingleFileAsync();

            OpenPreviewVideo();
        }
        private void sldPreview_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            SeekVideo(TimeSpan.FromMilliseconds(e.NewValue));
        }

        private async void OpenPreviewVideo()
        {
            if (m_video != null)
            {
                m_mediaReader = await MediaReader.CreateFromFileAsync(m_video);
                sldPreview.Maximum = m_mediaReader.Duration.TotalMilliseconds;
                sldPreview.Value = 10000;

                ShowPanel(gridRegionSelect);
            }
        }
        private async void SeekVideo(TimeSpan position)
        {
            m_mediaReader.Seek(position);
            using (var mediaResult = await m_mediaReader.VideoStream.ReadAsync())
            {
                var inputSample = (MediaSample2D)mediaResult.Sample;

                using (var outputSample = new MediaSample2D(MediaSample2DFormat.Nv12, inputSample.Width, inputSample.Height))
                using (var inputBuffer = inputSample.LockBuffer(BufferAccessMode.Read))
                using (var outputBuffer = outputSample.LockBuffer(BufferAccessMode.Write))
                {
                    // Wrap MediaBuffer2D in Bitmap
                    var inputBitmap = new Bitmap(
                        new Size(inputSample.Width, inputSample.Height),
                        ColorMode.Yuv420Sp,
                        new uint[] { inputBuffer.Planes[0].Pitch, inputBuffer.Planes[1].Pitch },
                        new IBuffer[] { inputBuffer.Planes[0].Buffer, inputBuffer.Planes[1].Buffer }
                        );
                    var outputBitmap = new Bitmap(
                        new Size(inputSample.Width, inputSample.Height),
                        ColorMode.Yuv420Sp,
                        new uint[] { outputBuffer.Planes[0].Pitch, outputBuffer.Planes[1].Pitch },
                        new IBuffer[] { outputBuffer.Planes[0].Buffer, outputBuffer.Planes[1].Buffer }
                        );

                    // Apply effect
                    var _grayscaleEffect = new GrayscaleEffect();
                    ((IImageConsumer)_grayscaleEffect).Source = new Lumia.Imaging.BitmapImageSource(inputBitmap);
                    m_renderer = new SwapChainPanelRenderer(_grayscaleEffect, swapChainPanelTarget);
                    await m_renderer.RenderAsync();
                }
            }
        }
        private void ShowPanel(Grid grid)
        {
            if (grid.Visibility == Visibility.Visible)
                return;

            foreach (Grid item in m_arrayGrids)
            {
                item.Visibility = Visibility.Collapsed;
            }

            grid.Visibility = Visibility.Visible;
        }
    }
}
