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
using Microsoft.Graphics.Canvas.Text;
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
                CanvasDevice device = new CanvasDevice();
                CanvasRenderTarget renderer = null;
                CanvasTextFormat font = null;
                Rect rtSource = Rect.Empty, rtDest = Rect.Empty, rtText = Rect.Empty;

                for (int i = 0; i < p.VideoFrames.Count; i++)
                {
                    using (IRandomAccessStream stream = await p.VideoFrames[i].ImageFile.OpenAsync(FileAccessMode.Read))
                    {
                        CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(device, stream);

                        if (rtSource.IsEmpty)
                            rtSource = rtText = rtDest = new Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height);
                        if (renderer == null)
                            renderer = new CanvasRenderTarget(device, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height * 2 * p.VideoFrames.Count, bitmap.Dpi);
                        if (font == null)
                            font = new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = (bitmap.SizeInPixels.Height - 2) * 76 / bitmap.Dpi };

                        using (CanvasDrawingSession ds = renderer.CreateDrawingSession())
                        {
                            rtText.Y = bitmap.SizeInPixels.Height * i * 2;
                            ds.FillRectangle(rtText, Colors.White);
                            ds.DrawText(string.Format("#{0}", i), rtText, Colors.Black, font);
                            rtDest.Y = (bitmap.SizeInPixels.Height * i * 2) + bitmap.SizeInPixels.Height;
                            ds.DrawImage(bitmap, rtDest, rtSource);
                        }
                    }
                }

                using (var outStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await renderer.SaveAsync(outStream, CanvasBitmapFileFormat.Jpeg);
                }
            }
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
        public object Convert(object value, Type targetType, object parameter, string language)
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
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
