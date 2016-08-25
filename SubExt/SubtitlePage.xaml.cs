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
using Windows.Media.Ocr;
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

        private CanvasBitmap m_bitmapEdit;
        private CanvasControl canvasControlEdit;
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
            {
                listSubtitles.DataContext = p.VideoFrames;
                listSubtitles.SelectedIndex = 0;
            }
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            gridEdit.Visibility = Visibility.Collapsed;
        }

        void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (canvasControlEdit != null)
            {
                canvasControlEdit.RemoveFromVisualTree();
                canvasControlEdit = null;
            }
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

            await frame.ImageFile.RenameAsync(((int)frame.BeginTime.TotalMilliseconds).ToString("D8") + "-" + ((int)frame.EndTime.TotalMilliseconds).ToString("D8") + ".bmp", NameCollisionOption.ReplaceExisting);
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

            //foreach (VideoFrame frame in listSubtitles.SelectedItems)
            //{
            //    p.VideoFrames.Remove(frame);
            //    await frame.ImageFile.DeleteAsync();
            //}
        }

        private async void buttonSaveAsSrt_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("SRT", new List<string>() { ".srt" });
            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = p.DisplayName;

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
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            folderPicker.FileTypeFilter.Add("*");
            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                // Application now has read/write access to all contents in the picked folder
                // (including other sub-folder contents)
                Windows.Storage.AccessCache.StorageApplicationPermissions.
                FutureAccessList.AddOrReplace("PickedFolderToken", folder);
            }
            else
            {
                return;
            }

            List<StorageFile> files = await SaveImagesAsync(folder);
        }

        private async void buttonStartOcr_Click(object sender, RoutedEventArgs e)
        {
            OcrEngine ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

            foreach (VideoFrame frame in p.VideoFrames)
            {
                SoftwareBitmap softwareBitmap;

                using (IRandomAccessStream stream = await frame.ImageFile.OpenAsync(FileAccessMode.Read))
                {
                    // Create the decoder from the stream
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                    // Get the SoftwareBitmap representation of the file
                    softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                    var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                    frame.Subtitle = ocrResult.Text.Replace(" ", "");
                }
            }
        }

        //private async void buttonStartOcr_Click(object sender, RoutedEventArgs e)
        //{
        //    UserCredential credential = null;
        //    try
        //    {
        //        credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
        //            new Uri("ms-appx:///Assets/client_secret.json"), 
        //            new[] { DriveService.Scope.DriveFile }, "user", CancellationToken.None);
        //    }
        //    catch (AggregateException ex)
        //    {
        //        Debug.Write("Credential failed, " + ex.Message);
        //    }

        //    // Create Drive API service.
        //    DriveService service = new DriveService(new BaseClientService.Initializer()
        //    {
        //        HttpClientInitializer = credential,
        //        ApplicationName = "Subtitle Extractor",
        //    });

        //    // First create the folder
        //    File folderMetadata = new File();
        //    folderMetadata.Name = p.Video.Name;
        //    folderMetadata.MimeType = "application/vnd.google-apps.folder";
        //    //folderMetadata.Parents = new List<string>() { "appDataFolder" };
        //    FilesResource.CreateRequest requestCreate = service.Files.Create(folderMetadata);
        //    requestCreate.Fields = "id";
        //    File folder = await requestCreate.ExecuteAsync();
        //    Debug.WriteLine("Folder ID: " + folder.Id);

        //    foreach (VideoFrame frame in p.VideoFrames)
        //    {
        //        try
        //        { 
        //            // First upload the image file
        //            File fileMetadata = new File();
        //            fileMetadata.Name = frame.ImageFile.Name;
        //            fileMetadata.Parents = new List<string> { folder.Id };
        //            FilesResource.CreateMediaUpload requestUpload;

        //            using (System.IO.FileStream stream = new System.IO.FileStream(frame.ImageFile.Path, System.IO.FileMode.Open))
        //            {
        //                requestUpload = service.Files.Create(fileMetadata, stream, "image/bmp");
        //                requestUpload.Fields = "id";
        //                requestUpload.Upload();
        //            }
        //            File imgFile = requestUpload.ResponseBody;
        //            Debug.WriteLine(string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss.fff"), imgFile.Id));

        //            // Then copy the image file as document
        //            File textMetadata = new File();
        //            textMetadata.Name = frame.ImageFile.Name;
        //            textMetadata.Parents = new List<string> { folder.Id };
        //            textMetadata.MimeType = "application/vnd.google-apps.document";
        //            FilesResource.CopyRequest requestCopy = service.Files.Copy(textMetadata, imgFile.Id);
        //            requestCopy.Fields = "id";
        //            requestCopy.OcrLanguage = "zh-TW";
        //            File textFile = await requestCopy.ExecuteAsync();

        //            // Finally export the document as text
        //            FilesResource.ExportRequest requestExport = service.Files.Export(textFile.Id, "text/plain");
        //            string text = await requestExport.ExecuteAsync();

        //            frame.Subtitle = text.Substring(text.LastIndexOf("\r\n\r\n") + 4);
        //            listSubtitles.SelectedItem = frame;
        //            listSubtitles.ScrollIntoView(frame);
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine(ex.Message);
        //        }
        //    }

        //    FilesResource.DeleteRequest requestDelete = service.Files.Delete(folder.Id);
        //    string result = await requestDelete.ExecuteAsync();
        //}

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
            }
            if (canvasControlEdit != null)
            {
                canvasControlEdit.RemoveFromVisualTree();
                canvasControlEdit = null;
            }
            p.RedoPixels.Clear();
            p.UndoPixels.Clear();

            gridEdit.Visibility = Visibility.Collapsed;            
        }

        private void imageSubtitle_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            VideoFrame frame = (sender as FrameworkElement)?.DataContext as VideoFrame;
            double w = canvasEdit.ActualWidth;
            canvasControlEdit = new CanvasControl
            {
                Width = w,
                Height = frame.ImageSize.Height / frame.ImageSize.Width * w,
                Name = "canvasControlEdit",
            };
            canvasControlEdit.Draw += canvasControlEdit_Draw;
            canvasControlEdit.CreateResources += canvasControlEdit_CreateResources;
            canvasControlEdit.PointerEntered += canvasControlEdit_PointerEntered;
            canvasControlEdit.PointerExited += canvasControlEdit_PointerExited;
            canvasControlEdit.PointerPressed += canvasControlEdit_PointerPressed;
            canvasControlEdit.PointerReleased += canvasControlEdit_PointerReleased;
            canvasControlEdit.PointerMoved += canvasControlEdit_PointerMoved;
            gridFrameEdit.Children.Add(canvasControlEdit);

            string value = (comboBoxPencilSize.SelectedValue as FrameworkElement).Tag.ToString();
            imagePencil.Source = new BitmapImage(new Uri(string.Format("ms-appx:///Images/Pencil_{0}.png", value)));

            rectFill.Width = rectFill.Height = 0;
            ptCanvasStart = new Point(-1, -1);
            gridEdit.Visibility = Visibility.Visible;
        }

        private void canvasControlEdit_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            imagePencil.Visibility = Visibility.Collapsed;
        }

        private void canvasControlEdit_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (buttonPencilFill.IsChecked == true)
            {
                imagePencil.Visibility = Visibility.Visible;
            }
        }

        private void canvasControlEdit_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint ptrPt = e.GetCurrentPoint(canvasControlEdit);
            Point pos = ptrPt.Position;
            pos.X += canvasControlEdit.Margin.Left;
            pos.Y += canvasControlEdit.Margin.Top;

            if (buttonRectangleFill.IsChecked == true)
            {
                if (!ptrPt.Properties.IsLeftButtonPressed || rectFill.Visibility == Visibility.Collapsed)
                    return;

                Rect rect;
                // Set the position of rectangle
                rect.X = Math.Min(pos.X, m_ptRegionStart.X);
                rect.Y = Math.Min(pos.Y, m_ptRegionStart.Y);

                // Set the dimenssion of the rectangle
                rect.Width = Math.Max(pos.X, m_ptRegionStart.X) - rect.X;
                rect.Height = Math.Max(pos.Y, m_ptRegionStart.Y) - rect.Y;

                if (rect.Right > canvasControlEdit.ActualWidth)
                    rect.Width -= rect.Right - canvasControlEdit.ActualWidth;
                if (rect.Bottom > canvasControlEdit.ActualHeight)
                    rect.Height -= rect.Bottom - canvasControlEdit.ActualHeight;

                Canvas.SetLeft(rectFill, rect.X);
                Canvas.SetTop(rectFill, rect.Y);
                rectFill.Width = rect.Width;
                rectFill.Height = rect.Height;
                Debug.WriteLine(string.Format("rect {0}, pos {1} start {2}", rect.ToString(), pos.ToString(), m_ptRegionStart.ToString()));
            }
            else if (buttonPencilFill.IsChecked == true)
            {
                // Set position of overlay
                int value = Convert.ToInt16((comboBoxPencilSize.SelectedValue as FrameworkElement).Tag);
                Point pt = Helper.GetPencilOffset(value, pos);
                Canvas.SetLeft(imagePencil, pt.X);
                Canvas.SetTop(imagePencil, pt.Y);

                // If button pressed, we will fill color
                if (!ptrPt.Properties.IsLeftButtonPressed || imagePencil.Visibility == Visibility.Collapsed)
                    return;

                Point ptFill = ptCanvasStart = new Point((uint)(m_bitmapEdit.SizeInPixels.Width / canvasControlEdit.ActualWidth * ptrPt.Position.X), (uint)(m_bitmapEdit.SizeInPixels.Height / canvasControlEdit.ActualHeight * ptrPt.Position.Y));
                Color[] pixels = m_bitmapEdit.GetPixelColors();

                int size = Convert.ToInt16((comboBoxPencilSize.SelectedItem as FrameworkElement).Tag);
                bool result = Helper.PencilFill(pixels, (int)m_bitmapEdit.SizeInPixels.Width, (int)m_bitmapEdit.SizeInPixels.Height, ptFill, size, Colors.White);
                
                Debug.WriteLine(string.Format("pt {0} size {1}", ptFill.ToString(), size));
                m_bitmapEdit.SetPixelColors(pixels);
                canvasControlEdit.Invalidate();
            }
        }

        private void canvasControlEdit_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (m_bitmapEdit == null)
                return;

            PointerPoint ptrPt = e.GetCurrentPoint(canvasControlEdit);
            Point ptCanvasEnd = new Point((uint)(m_bitmapEdit.SizeInPixels.Width / canvasControlEdit.ActualWidth * ptrPt.Position.X), (uint)(m_bitmapEdit.SizeInPixels.Height / canvasControlEdit.ActualHeight * ptrPt.Position.Y));

            if (buttonRectangleFill.IsChecked == true)
            {
                if ((ptCanvasStart.X == -1 && ptCanvasStart.Y == -1) || ptCanvasStart == ptCanvasEnd || rectFill.Visibility == Visibility.Collapsed)
                    return;

                Color[] pixels = m_bitmapEdit.GetPixelColors();
                Color[] undo = (Color[])pixels.Clone();

                if (Helper.RectangleFill(pixels, (int)m_bitmapEdit.SizeInPixels.Width, (int)m_bitmapEdit.SizeInPixels.Height, ptCanvasStart, ptCanvasEnd, Colors.White))
                {
                    if (p.UndoPixels.Count == 5)
                        p.UndoPixels.RemoveAt(0);
                    p.UndoPixels.Add(undo);
                }

                m_bitmapEdit.SetPixelColors(pixels);
                canvasControlEdit.Invalidate();
            }

            canvasControlEdit.ReleasePointerCapture(e.Pointer);
        }

        private void canvasControlEdit_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (m_bitmapEdit == null)
                return;

            canvasControlEdit.CapturePointer(e.Pointer);

            rectFill.Width = rectFill.Height = 0;

            PointerPoint ptrPt = e.GetCurrentPoint(canvasControlEdit);
            m_ptRegionStart = ptrPt.Position;
            m_ptRegionStart.X += canvasControlEdit.Margin.Left;
            m_ptRegionStart.Y += canvasControlEdit.Margin.Top;
            ptCanvasStart = new Point((uint)(m_bitmapEdit.SizeInPixels.Width / canvasControlEdit.ActualWidth * ptrPt.Position.X), (uint)(m_bitmapEdit.SizeInPixels.Height / canvasControlEdit.ActualHeight * ptrPt.Position.Y));
            
            if (buttonFloodFill.IsChecked == true)
            {
                Color[] pixels = m_bitmapEdit.GetPixelColors();
                Color[] undo = (Color[])pixels.Clone();

                if (Helper.FloodFill(pixels, (int)m_bitmapEdit.SizeInPixels.Width, (int)m_bitmapEdit.SizeInPixels.Height, ptCanvasStart, Colors.Black, Colors.White))
                {
                    if (p.UndoPixels.Count == 5)
                        p.UndoPixels.RemoveAt(0);
                    p.UndoPixels.Add(undo);
                }

                m_bitmapEdit.SetPixelColors(pixels);
                canvasControlEdit.Invalidate();
            }
            else if (buttonRectangleFill.IsChecked == true)
            {
                Canvas.SetLeft(rectFill, ptrPt.Position.X);
                Canvas.SetTop(rectFill, ptrPt.Position.Y);
            }
            else if (buttonPencilFill.IsChecked == true)
            {
                Color[] pixels = m_bitmapEdit.GetPixelColors();
                Color[] undo = (Color[])pixels.Clone();

                int size = Convert.ToInt16((comboBoxPencilSize.SelectedItem as FrameworkElement).Tag);
                if (Helper.PencilFill(pixels, (int)m_bitmapEdit.SizeInPixels.Width, (int)m_bitmapEdit.SizeInPixels.Height, ptCanvasStart, size, Colors.White))
                {
                    if (p.UndoPixels.Count == 5)
                        p.UndoPixels.RemoveAt(0);
                    p.UndoPixels.Add(undo);
                }

                m_bitmapEdit.SetPixelColors(pixels);
                canvasControlEdit.Invalidate();
            }
        }  
        private void canvasControlEdit_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            // Create any resources needed by the Draw event handler.
            // Asynchronous work can be tracked with TrackAsyncAction:
            args.TrackAsyncAction(canvasControlEdit_CreateResourcesAsync(sender).AsAsyncAction());
        }
        private async Task canvasControlEdit_CreateResourcesAsync(CanvasControl sender)
        {
            using (IRandomAccessStream stream = await p.SelectedImageFile?.OpenAsync(FileAccessMode.Read))
            {
                if (m_bitmapEdit == null)
                {
                    m_bitmapEdit = await CanvasBitmap.LoadAsync(sender, stream);
                }
            }
        }
        private void canvasControlEdit_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
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

        private void buttonRedo_Click(object sender, RoutedEventArgs e)
        {
            if (p.RedoPixels.Count > 0)
            {
                int lastIndex = p.RedoPixels.Count - 1;
                m_bitmapEdit.SetPixelColors(p.RedoPixels[lastIndex]);
                canvasControlEdit.Invalidate();

                if (p.UndoPixels.Count == 5)
                    p.UndoPixels.RemoveAt(0);
                p.UndoPixels.Add(p.RedoPixels[lastIndex]);
                p.RedoPixels.RemoveAt(lastIndex);
            }
        }
        private void buttonUndo_Click(object sender, RoutedEventArgs e)
        {
            if (p.UndoPixels.Count > 0)
            {
                int lastIndex = p.UndoPixels.Count - 1;
                m_bitmapEdit.SetPixelColors(p.UndoPixels[lastIndex]);
                canvasControlEdit.Invalidate();

                if (p.RedoPixels.Count == 5)
                    p.RedoPixels.RemoveAt(0);
                p.RedoPixels.Add(p.UndoPixels[lastIndex]);
                p.UndoPixels.RemoveAt(lastIndex);
            }
        }

        private void comboBoxPencilSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (imagePencil != null)
            {
                string value = (comboBoxPencilSize.SelectedValue as FrameworkElement).Tag.ToString();
                imagePencil.Source = new BitmapImage(new Uri(string.Format("ms-appx:///Images/Pencil_{0}.png", value)));
            }
        }

        private void listSubtitles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VideoFrame f = listSubtitles.SelectedItem as VideoFrame;
            p.SelectedImageFile = f?.ImageFile;            
        }

        private async Task<List<StorageFile>> SaveImagesAsync(StorageFolder folder)
        {
            List<StorageFile> files = new List<StorageFile>();
            
            // Initialize device
            CanvasDevice device = new CanvasDevice();
            CanvasRenderTarget renderer = null;
            CanvasTextFormat font = null;
            Rect rtSource = Rect.Empty, rtDest = Rect.Empty, rtText = Rect.Empty;
            int fileCount = 0;

            StorageFile file = await folder.CreateFileAsync(p.DisplayName + "." + fileCount.ToString() + ".jpg", CreationCollisionOption.ReplaceExisting);

            int subCount = 0;
            int maxSubPerImage = 0;
            for (int i = 0; i < p.VideoFrames.Count; i++)
            {
                if (file == null)
                {
                    file = await folder.CreateFileAsync(p.DisplayName + "." + fileCount.ToString() + ".jpg", CreationCollisionOption.ReplaceExisting);
                    subCount = 0;

                    renderer = null;
                    rtSource = Rect.Empty;
                }

                using (IRandomAccessStream stream = await p.VideoFrames[i].ImageFile.OpenAsync(FileAccessMode.Read))
                using (CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(device, stream))
                {
                    if (maxSubPerImage == 0)
                        maxSubPerImage = (int)(device.MaximumBitmapSizeInPixels / bitmap.SizeInPixels.Height / 2 / 4);

                    if (rtSource.IsEmpty)
                        rtSource = rtText = rtDest = new Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height);
                    if (renderer == null)
                        renderer = new CanvasRenderTarget(device, bitmap.SizeInPixels.Width, device.MaximumBitmapSizeInPixels / 4, bitmap.Dpi);
                    if (font == null)
                        font = new CanvasTextFormat { FontFamily = "Courier New", FontSize = (bitmap.SizeInPixels.Height * 0.75f) * 76 / bitmap.Dpi, HorizontalAlignment = CanvasHorizontalAlignment.Center };

                    using (CanvasDrawingSession ds = renderer.CreateDrawingSession())
                    {
                        rtText.Y = bitmap.SizeInPixels.Height * subCount * 2;// + (bitmap.SizeInPixels.Height * 0.25f);
                        ds.FillRectangle(rtText, Colors.White);
                        rtText.Y += (bitmap.SizeInPixels.Height * 0.4f);
                        rtText.Height = bitmap.SizeInPixels.Height * 0.2f;
                        ds.FillRectangle(rtText, Colors.Black);
                        rtText.Y += rtText.Height;
                        rtText.Height = bitmap.SizeInPixels.Height * 0.4f;
                        ds.FillRectangle(rtText, Colors.White);
                        //ds.DrawText(string.Format("<{0}>", i), rtText, Colors.Black, font);
                        rtDest.Y = (bitmap.SizeInPixels.Height * subCount * 2) + bitmap.SizeInPixels.Height;
                        ds.DrawImage(bitmap, rtDest, rtSource);
                        subCount++;
                    }
                }

                if (subCount == maxSubPerImage || i == p.VideoFrames.Count - 1)
                {
                    using (IRandomAccessStream outStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await renderer.SaveAsync(outStream, CanvasBitmapFileFormat.Jpeg);
                        files.Add(file);
                        file = null;
                        fileCount++;
                    }
                }
            }

            return files;
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
