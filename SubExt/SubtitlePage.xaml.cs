using System;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI;
using SubExt.Model;
using SubExt.ViewModel;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SubExt
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SubtitlePage : Page
    {
        private Payload p;
        public SubtitlePage()
        {
            this.InitializeComponent();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            p = e.Parameter as Payload;
            ItemViewOnPage.DataContext = p.VideoFrames;
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            
        }

        private void buttonInsert_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Button item = sender as Button;
            VideoFrame frame = item.DataContext as VideoFrame;

            int curIdx = p.VideoFrames.IndexOf(frame);
            VideoFrame framePrev = p.VideoFrames[curIdx - 1];
            VideoFrame frameNew = new VideoFrame() {
                BeginTime = new TimeSpan(framePrev.EndTime.Ticks + 1),
                EndTime = new TimeSpan(frame.BeginTime.Ticks - 1),
            };
            p.VideoFrames.Insert(curIdx, frameNew);
        }

        private void buttonMergeUp_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Button item = sender as Button;
            VideoFrame frame = item.DataContext as VideoFrame;

            int curIdx = p.VideoFrames.IndexOf(frame);
            VideoFrame framePrev = p.VideoFrames[curIdx - 1];
            frame.BeginTime = framePrev.BeginTime;
            p.VideoFrames.Remove(framePrev);
        }

        private void buttonMergeDown_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Button item = sender as Button;
            VideoFrame frame = item.DataContext as VideoFrame;

            int curIdx = p.VideoFrames.IndexOf(frame);
            VideoFrame frameNext = p.VideoFrames[curIdx + 1];
            frame.EndTime = frameNext.EndTime;
            p.VideoFrames.Remove(frameNext);
        }

        private void buttonDelete_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Button item = sender as Button;
            p.VideoFrames.Remove(item.DataContext as VideoFrame);
        }

        private async void buttonSaveAsSrt_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("SRT", new List<string>() { ".srt" });
            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = p.Video.DisplayName;

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                for (int i = 0; i < p.VideoFrames.Count; i++)
                {
                    string subtitle = i.ToString() + Environment.NewLine;
                    subtitle += p.VideoFrames[i].BeginTime.ToString(@"hh\:mm\:ss\,fff") + " --> " + p.VideoFrames[i].EndTime.ToString(@"hh\:mm\:ss\,fff") + Environment.NewLine;
                    subtitle += p.VideoFrames[i].Subtitle + Environment.NewLine + Environment.NewLine;
                    await FileIO.AppendTextAsync(file, subtitle, UnicodeEncoding.Utf8);
                }
            }
        }

        private async void buttonSaveAsImg_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("JPG", new List<string>() { ".jpg" });
            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = p.Video.DisplayName;

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {

                using (var stream0 = await p.VideoFrames[0].ImageFile.OpenAsync(FileAccessMode.Read))
                using (var stream1 = await p.VideoFrames[1].ImageFile.OpenAsync(FileAccessMode.Read))
                {
                    var device = new CanvasDevice();
                    var bitmap0 = await CanvasBitmap.LoadAsync(device, stream0);
                    var bitmap1 = await CanvasBitmap.LoadAsync(device, stream1);

                    

                    // Initialize to solid gray.
                    var bitmap = CanvasBitmap.CreateFromColors(device, new Color[] { Colors.Gray, Colors.Black, Colors.Black, Colors.Gray }, (int)bitmap0.SizeInPixels.Width, (int)bitmap0.SizeInPixels.Height);
                    var renderer = new CanvasRenderTarget(device, bitmap0.SizeInPixels.Width, bitmap0.SizeInPixels.Height * 2, bitmap0.Dpi);

                    using (CanvasDrawingSession ds = renderer.CreateDrawingSession())
                    {
                        //var blur = new GaussianBlurEffect();
                        //blur.BlurAmount = 8.0f;
                        //blur.BorderMode = EffectBorderMode.Hard;
                        //blur.Optimization = EffectOptimization.Quality;
                        //blur.Source = bitmap;
                        var blend = new BlendEffect()
                        {
                            Background = bitmap,
                            Foreground = bitmap1,
                            Mode = BlendEffectMode.Color
                        };
                        Rect rt = new Rect(0, bitmap0.SizeInPixels.Height, bitmap0.SizeInPixels.Width, bitmap0.SizeInPixels.Height);
                        float ft = 0.5f;
                        ds.DrawImage(blend);
                    }

                    using (var outStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await renderer.SaveAsync(outStream, CanvasBitmapFileFormat.Jpeg);
                    }
                }
            }
            //p.VideoFrames[0].ImageSize;


            //file.OpenReadAsync()) { WriteableBitmap bitmap = new WriteableBitmap(width, height); await bitmap.SetSourceAsync(stream); }


            //BitmapImage bitmap = new BitmapImage(new Uri("YourImage.jpg", UriKind.Relative));
            //WriteableBitmap writeableBitmap = new WriteableBitmap(bitmap);
            //// Say this is the image bytes...
            //var imageBytes = MyBitmap.PixelBuffer.ToArray();
            //// Create an inmemory RAS
            //var inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
            //// Write the bytes to the stream..
            //await(inMemoryRandomAccessStream.AsStreamForWrite()).WriteAsync(imageBytes, 0, imageBytes.Length);
            //// Flush the bytes
            //await inMemoryRandomAccessStream.FlushAsync();
            //// Set back the position
            //inMemoryRandomAccessStream.Seek(0);
        }
    }

    public class TimeSpanFormatter : IValueConverter
    {
        // This converts the value object to Visibility.
        // This will work with most simple types.
        public object Convert(object value, Type targetType,
            object parameter, string language)
        {
            TimeSpan ts = value == null ? new TimeSpan() : (TimeSpan)value;            
            return ts.ToString(@"hh\:mm\:ss\,fff");
        }

        // No need to implement converting back on a one-way binding
        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ImageSourceFormatter : IValueConverter
    {
        static private CoreDispatcher UIdispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
        static public IAsyncAction UIThreadAsync(DispatchedHandler function)
        {
            return UIdispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, function);
        }

        // This converts the value object to Visibility.
        // This will work with most simple types.
        public object Convert(object value, Type targetType,
            object parameter, string language)
        {
            if (value != null)
            {
                StorageFile file = value as StorageFile;

                BitmapImage bmp = new BitmapImage();
                file.OpenAsync(FileAccessMode.Read).Completed = new AsyncOperationCompletedHandler<IRandomAccessStream>((openInfo, openStatus) =>
                {
                    if (openStatus == AsyncStatus.Completed)
                    {
                        UIThreadAsync(() =>
                        {
                            IRandomAccessStream stream = openInfo.GetResults();
                            bmp.SetSource(stream);
                        });
                    }
                });
                return bmp;
            }
            return null;
        }

        // No need to implement converting back on a one-way binding
        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
