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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
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

        private byte[] m_pixels;
        private CanvasBitmap m_bitmapEdit;
        private CanvasRenderTarget offscreen;

        public SubtitlePage()
        {
            this.InitializeComponent();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            p = e.Parameter as Payload;
            if (p?.VideoFrames != null)
                ItemViewOnPage.DataContext = p.VideoFrames;
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            
        }

        void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            this.canvasEdit.RemoveFromVisualTree();
            this.canvasEdit = null;
        }

        private void buttonInsert_Click(object sender, RoutedEventArgs e)
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

        private void buttonMergeUp_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;
            VideoFrame frame = item.DataContext as VideoFrame;

            int curIdx = p.VideoFrames.IndexOf(frame);
            VideoFrame framePrev = p.VideoFrames[curIdx - 1];
            frame.BeginTime = framePrev.BeginTime;
            p.VideoFrames.Remove(framePrev);
        }

        private void buttonMergeDown_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;
            VideoFrame frame = item.DataContext as VideoFrame;

            int curIdx = p.VideoFrames.IndexOf(frame);
            VideoFrame frameNext = p.VideoFrames[curIdx + 1];
            frame.EndTime = frameNext.EndTime;
            p.VideoFrames.Remove(frameNext);
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            Button item = sender as Button;
            p.VideoFrames.Remove(item.DataContext as VideoFrame);
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

        private void buttonCloseSelctedImage_Click(object sender, RoutedEventArgs e)
        {
            p.SelectedImageFile = null;
            gridEdit.Visibility = Visibility.Collapsed;
        }

        private async void imageSubtitle_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            Image i = sender as Image;
            VideoFrame f = i.DataContext as VideoFrame;
            p.SelectedImageFile = f.ImageFile;

            gridEdit.Visibility = Visibility.Visible;
            canvasEdit.Invalidate();
        }

        private async void imageEdit_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint ptrPt = e.GetCurrentPoint(imageEdit);
            Image img = sender as Image;
            BitmapSource bmp = img.Source as BitmapSource;
            Point ptSource = new Point((uint)(bmp.PixelWidth / img.ActualWidth * ptrPt.Position.X), (uint)(bmp.PixelHeight / img.ActualHeight * ptrPt.Position.Y));

            Helper.FloodFill(m_pixels, bmp.PixelWidth, bmp.PixelHeight, ptSource, Colors.Black, Colors.White);

            StorageFile file = p.SelectedImageFile;
            p.SelectedImageFile = null;
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var propertySet = new BitmapPropertySet();
                propertySet.Add("ImageQuality", new BitmapTypedValue(1.0, PropertyType.Single));
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                    (uint)bmp.PixelWidth, (uint)bmp.PixelHeight,
                    96.0, 96.0, m_pixels);
                await encoder.FlushAsync();
                p.SelectedImageFile = file;
            }
        }        
        private async void imageEdit_ImageOpened(object sender, RoutedEventArgs e)
        {
            using (IRandomAccessStream stream = await p.SelectedImageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                Image img = sender as Image;
                BitmapSource bmp = img.Source as BitmapSource;
                WriteableBitmap wb = new WriteableBitmap(bmp.PixelWidth, bmp.PixelHeight);
                await wb.SetSourceAsync(stream);
                m_pixels = wb.PixelBuffer.ToArray();
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
            //using (IRandomAccessStream stream = await p.SelectedImageFile?.OpenAsync(FileAccessMode.Read))
            //if (m_bitmapEdit == null)
            //{
            //    m_bitmapEdit = await CanvasBitmap.LoadAsync(sender, ApplicationData.Current.TemporaryFolder.Path + "\\00000533-00002769.bmp");
            //}
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            offscreen = new CanvasRenderTarget(device, 128, 128, 96);
            using (CanvasDrawingSession ds = offscreen.CreateDrawingSession())
            {
                ds.Clear(Colors.Black);
                ds.DrawRectangle(10, 10, 50, 60, Colors.Red);
            }
        }

        private void canvasEdit_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            //Rect rtSource = new Rect(0, 0, m_bitmapEdit.SizeInPixels.Width, m_bitmapEdit.SizeInPixels.Height);
            //            args.DrawingSession.DrawImage(m_bitmapEdit, 0, 0, rtSource);
            args.DrawingSession.DrawImage(offscreen, 23, 34);
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
