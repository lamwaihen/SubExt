using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using SubExt.Model;
using SubExt.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Windows.UI;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SubExt
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SubtitlePage : Page
    {
        private Payload p;

        private Color[] m_pixels;
        private CanvasBitmap m_bitmapEdit;
        private CanvasControl canvasControlEdit;
        private Rectangle rectFill;
        private Point ptCanvasStart;
        private Point m_ptRegionStart;

        public SubtitlePage()
        {
            this.InitializeComponent();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            p = e.Parameter as Payload;
            if (p?.VideoFrames != null)
                listSubtitles.DataContext = p.VideoFrames;
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            
        }

        void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            canvasControlEdit.RemoveFromVisualTree();
            canvasControlEdit = null;
        }

        private void buttonInsert_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;
            RelativePanel panel = item.Parent as RelativePanel;
            EnableEditControls(panel, false);
            VideoFrame frame = item.DataContext as VideoFrame;

            int curIdx = p.VideoFrames.IndexOf(frame);
            VideoFrame framePrev = p.VideoFrames[curIdx - 1];
            VideoFrame frameNew = new VideoFrame() {
                BeginTime = new TimeSpan(framePrev.EndTime.Ticks + 1),
                EndTime = new TimeSpan(frame.BeginTime.Ticks - 1),
            };
            p.VideoFrames.Insert(curIdx, frameNew);
            EnableEditControls(panel, true);
        }

        private async void buttonMergeUp_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;
            RelativePanel panel = item.Parent as RelativePanel;
            EnableEditControls(panel, false);
            VideoFrame frame = item.DataContext as VideoFrame;

            int curIdx = p.VideoFrames.IndexOf(frame);
            VideoFrame framePrev = p.VideoFrames[curIdx - 1];
            frame.BeginTime = framePrev.BeginTime;
            p.VideoFrames.Remove(framePrev);

            using (IRandomAccessStream stream1 = await framePrev.ImageFile.OpenAsync(FileAccessMode.Read))
            using (CanvasBitmap bmp1 = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream1))
            using (IRandomAccessStream stream2 = await frame.ImageFile.OpenAsync(FileAccessMode.ReadWrite))
            using (CanvasBitmap bmp2 = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream2))
            {
                Color[] pixels1 = bmp1.GetPixelColors();
                Color[] pixels2 = bmp2.GetPixelColors();

                for (int i = 0; i < pixels1.Length; i++)
                {
                    if (pixels1[i] == Colors.Black)
                        pixels2[i] = pixels1[i];
                }

                bmp2.SetPixelColors(pixels2);
                await bmp2.SaveAsync(stream2, CanvasBitmapFileFormat.Bmp);
            }

            await frame.ImageFile.RenameAsync(((int)frame.BeginTime.TotalMilliseconds).ToString("D8") + "-" + ((int)frame.EndTime.TotalMilliseconds).ToString("D8") + ".bmp");
            await framePrev.ImageFile.DeleteAsync();

            // Force to refresh UI
            frame.ImageFile = frame.ImageFile;
            EnableEditControls(panel, true);
        }

        private async void buttonMergeDown_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;
            RelativePanel panel = item.Parent as RelativePanel;
            EnableEditControls(panel, false);
            VideoFrame frame = item.DataContext as VideoFrame;

            int curIdx = p.VideoFrames.IndexOf(frame);
            VideoFrame frameNext = p.VideoFrames[curIdx + 1];
            frame.EndTime = frameNext.EndTime;
            p.VideoFrames.Remove(frameNext);

            using (IRandomAccessStream stream1 = await frame.ImageFile.OpenAsync(FileAccessMode.ReadWrite))
            using (CanvasBitmap bmp1 = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream1))
            using (IRandomAccessStream stream2 = await frameNext.ImageFile.OpenAsync(FileAccessMode.Read))
            using (CanvasBitmap bmp2 = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream2)) 
            {
                Color[] pixels1 = bmp1.GetPixelColors();
                Color[] pixels2 = bmp2.GetPixelColors();

                for (int i = 0; i < pixels1.Length; i++)
                {
                    if (pixels2[i] == Colors.Black)
                        pixels1[i] = pixels2[i];
                }

                bmp1.SetPixelColors(pixels1);
                await bmp1.SaveAsync(stream1, CanvasBitmapFileFormat.Bmp);
            }

            await frame.ImageFile.RenameAsync(((int)frame.BeginTime.TotalMilliseconds).ToString("D8") + "-" + ((int)frame.EndTime.TotalMilliseconds).ToString("D8") + ".bmp");
            await frameNext.ImageFile.DeleteAsync();

            // Force to refresh UI
            frame.ImageFile = frame.ImageFile;
            EnableEditControls(panel, true);
        }

        private async void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;
            RelativePanel panel = item.Parent as RelativePanel;
            EnableEditControls(panel, false);
            VideoFrame frame = item.DataContext as VideoFrame;
            p.VideoFrames.Remove(frame);

            await frame.ImageFile.DeleteAsync();
            EnableEditControls(panel, true);
        }

        private async void buttonSaveAsSrt_Click(object sender, RoutedEventArgs e)
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

        private async void buttonSaveAsImg_Click(object sender, RoutedEventArgs e)
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

        private async void buttonStartOcr_Click(object sender, RoutedEventArgs e)
        {
            UserCredential credential = null;
            try
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new Uri("ms-appx:///Assets/client_secret.json"), 
                    new[] { DriveService.Scope.DriveFile }, "user", CancellationToken.None);
            }
            catch (AggregateException ex)
            {
                Debug.Write("Credential failed, " + ex.Message);
            }

            // Create Drive API service.
            DriveService service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Subtitle Extractor",
            });

            // First create the folder
            File folderMetadata = new File();
            folderMetadata.Name = p.Video.Name;
            folderMetadata.MimeType = "application/vnd.google-apps.folder";
            //folderMetadata.Parents = new List<string>() { "appDataFolder" };
            FilesResource.CreateRequest requestCreate = service.Files.Create(folderMetadata);
            requestCreate.Fields = "id";
            File folder = await requestCreate.ExecuteAsync();
            Debug.WriteLine("Folder ID: " + folder.Id);

            foreach (VideoFrame frame in p.VideoFrames)
            {
                // First upload the image file
                File fileMetadata = new File();
                fileMetadata.Name = frame.ImageFile.Name;
                fileMetadata.Parents = new List<string> { folder.Id };
                FilesResource.CreateMediaUpload requestUpload;

                using (System.IO.FileStream stream = new System.IO.FileStream(frame.ImageFile.Path, System.IO.FileMode.Open))
                {
                    requestUpload = service.Files.Create(fileMetadata, stream, "image/bmp");
                    requestUpload.Fields = "id";
                    requestUpload.Upload();
                }
                File imgFile = requestUpload.ResponseBody;
                Debug.WriteLine(string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss.fff"), imgFile.Id));

                // Then copy the image file as document
                File textMetadata = new File();
                textMetadata.Name = frame.ImageFile.Name;
                textMetadata.Parents = new List<string> { folder.Id };
                textMetadata.MimeType = "application/vnd.google-apps.document";
                FilesResource.CopyRequest requestCopy = service.Files.Copy(textMetadata, imgFile.Id);
                requestCopy.Fields = "id";
                requestCopy.OcrLanguage = "zh";
                File textFile = await requestCopy.ExecuteAsync();

                // Finally export the document as text
                FilesResource.ExportRequest requestExport = service.Files.Export(textFile.Id, "text/plain");
                string text = await requestExport.ExecuteAsync();
                frame.Subtitle = text.Substring(text.LastIndexOf("\r\n\r\n") + 4);
            }

            FilesResource.DeleteRequest requestDelete = service.Files.Delete(folder.Id);
            string result = await requestDelete.ExecuteAsync();
        }

        private async void buttonCloseSelctedImage_Click(object sender, RoutedEventArgs e)
        {
            if (p.SelectedImageFile != null)
            {
                using (IRandomAccessStream stream = await p.SelectedImageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await m_bitmapEdit.SaveAsync(stream, CanvasBitmapFileFormat.Bmp);
                    m_bitmapEdit = null;
                }

                // Force to refresh UI
                VideoFrame frame = listSubtitles.SelectedItem as VideoFrame;
                frame.ImageFile = frame.ImageFile;

                p.SelectedImageFile = null;
            }
            if (canvasControlEdit != null)
            {
                canvasControlEdit.RemoveFromVisualTree();
                canvasControlEdit = null;
            }
            if (rectFill != null)
            {
                canvasEdit.Children.Remove(rectFill);
                rectFill = null;
            }
            gridEdit.Visibility = Visibility.Collapsed;            
        }

        private void imageSubtitle_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            Image i = sender as Image;
            VideoFrame f = i.DataContext as VideoFrame;
            p.SelectedImageFile = f.ImageFile;

            double w = ActualWidth - gridEdit.Margin.Left - gridEdit.Margin.Right;
            canvasControlEdit = new CanvasControl {
                Width = w,
                Height = p.SubtitleRect.Height / p.SubtitleRect.Width * w,
                Name = "canvasEdit",
                Margin = new Thickness(4),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xB2, 0xD4, 0xEF))
            };
            canvasControlEdit.CreateResources += canvasEdit_CreateResources;
            canvasControlEdit.Draw += canvasEdit_Draw;
            canvasControlEdit.PointerPressed += canvasEdit_PointerPressed;
            canvasControlEdit.PointerReleased += canvasEdit_PointerReleased;
            canvasControlEdit.PointerMoved += canvasEdit_PointerMoved;
            canvasEdit.Children.Add(canvasControlEdit);

            rectFill = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0, 0)), Visibility = Visibility.Collapsed };
            Canvas.SetZIndex(rectFill, 1);
            canvasEdit.Children.Add(rectFill);            

            buttonFloodFill.IsChecked = false;
            buttonRectangleFill.IsChecked = false;

            gridEdit.Visibility = Visibility.Visible;
        }

        private void canvasEdit_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint ptrPt = e.GetCurrentPoint(canvasControlEdit);
            if (!ptrPt.Properties.IsLeftButtonPressed || rectFill.Visibility == Visibility.Collapsed)
                return;

            Point pos = ptrPt.Position;
            pos.X += canvasControlEdit.Margin.Left;
            pos.Y += canvasControlEdit.Margin.Top;

            // Set the position of rectangle
            var x = Math.Min(pos.X, m_ptRegionStart.X);
            var y = Math.Min(pos.Y, m_ptRegionStart.Y);

            // Set the dimenssion of the rectangle
            var w = Math.Max(pos.X, m_ptRegionStart.X) - x;
            var h = Math.Max(pos.Y, m_ptRegionStart.Y) - y;

            Canvas.SetLeft(rectFill, x);
            Canvas.SetTop(rectFill, y);
            rectFill.Width = w;
            rectFill.Height = h;
        }

        private void canvasEdit_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint ptrPt = e.GetCurrentPoint(canvasControlEdit);
            Point ptCanvasEnd = new Point((uint)(m_bitmapEdit.SizeInPixels.Width / canvasControlEdit.ActualWidth * ptrPt.Position.X), (uint)(m_bitmapEdit.SizeInPixels.Height / canvasControlEdit.ActualHeight * ptrPt.Position.Y));

            if (buttonRectangleFill.IsChecked == true)
            {
                if (ptCanvasStart == ptCanvasEnd)
                    return;

                m_pixels = m_bitmapEdit.GetPixelColors();
                Helper.RectangleFill(m_pixels, (int)m_bitmapEdit.SizeInPixels.Width, (int)m_bitmapEdit.SizeInPixels.Height, ptCanvasStart, ptCanvasEnd, Colors.White);

                m_bitmapEdit.SetPixelColors(m_pixels);
                canvasControlEdit.Invalidate();
            }
        }

        private void canvasEdit_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            rectFill.Width = rectFill.Height = 0;
            rectFill.Visibility = Visibility.Collapsed;

            PointerPoint ptrPt = e.GetCurrentPoint(canvasControlEdit);
            m_ptRegionStart = ptrPt.Position;
            m_ptRegionStart.X += canvasControlEdit.Margin.Left;
            m_ptRegionStart.Y += canvasControlEdit.Margin.Top;
            ptCanvasStart = new Point((uint)(m_bitmapEdit.SizeInPixels.Width / canvasControlEdit.ActualWidth * ptrPt.Position.X), (uint)(m_bitmapEdit.SizeInPixels.Height / canvasControlEdit.ActualHeight * ptrPt.Position.Y));
            
            if (buttonFloodFill.IsChecked == true)
            {
                m_pixels = m_bitmapEdit.GetPixelColors();
                Helper.FloodFill(m_pixels, (int)m_bitmapEdit.SizeInPixels.Width, (int)m_bitmapEdit.SizeInPixels.Height, ptCanvasStart, Colors.Black, Colors.White);

                m_bitmapEdit.SetPixelColors(m_pixels);
                canvasControlEdit.Invalidate();
            }
            else if (buttonRectangleFill.IsChecked == true)
            {
                Canvas.SetLeft(rectFill, ptrPt.Position.X);
                Canvas.SetTop(rectFill, ptrPt.Position.Y);
                rectFill.Visibility = Visibility.Visible;
            }
        }  
        private void canvasEdit_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            // Create any resources needed by the Draw event handler.
            // Asynchronous work can be tracked with TrackAsyncAction:
            args.TrackAsyncAction(canvasEdit_CreateResourcesAsync(sender).AsAsyncAction());
        }
        private async Task canvasEdit_CreateResourcesAsync(CanvasControl sender)
        {
            using (IRandomAccessStream stream = await p.SelectedImageFile?.OpenAsync(FileAccessMode.Read))
            {
                if (m_bitmapEdit == null)
                {
                    m_bitmapEdit = await CanvasBitmap.LoadAsync(sender, stream);
                }
            }
        }
        private async void canvasEdit_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (m_bitmapEdit == null)
            {
                using (IRandomAccessStream stream = await p.SelectedImageFile?.OpenAsync(FileAccessMode.Read))
                {
                    m_bitmapEdit = await CanvasBitmap.LoadAsync(sender, stream);
                }
            }
            Rect rtSource = new Rect(0, 0, m_bitmapEdit.SizeInPixels.Width, m_bitmapEdit.SizeInPixels.Height);
            Rect rtDest = new Rect(0, 0, canvasControlEdit.ActualWidth, canvasControlEdit.ActualHeight);
            args.DrawingSession.DrawImage(m_bitmapEdit, rtDest, rtSource);
        }

        private void EnableEditControls(RelativePanel panel, bool enable)
        {
            foreach (var item in panel.Children)
            {
                if (item is Button)
                {
                    (item as Button).IsEnabled = enable;
                }
            }
        }

        private void buttonFloodFill_Click(object sender, RoutedEventArgs e)
        {
            buttonRectangleFill.IsChecked = false;
        }

        private void buttonRectangleFill_Click(object sender, RoutedEventArgs e)
        {
            buttonFloodFill.IsChecked = false;
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
