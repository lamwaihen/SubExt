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
using System.Xml;
using Windows.Storage;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System.Threading;
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
        private OcrEngine m_ocrEngine;
        private XmlReader m_xmlReader;
        private ThreadPoolTimer m_saveXmlTimer;
        private StorageFolder m_folder;

        public SubtitlePage()
        {
            this.InitializeComponent();

            m_ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        }
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            p = e.Parameter as Payload;
            if (p?.VideoFrames != null)
            {
                listSubtitles.DataContext = p.VideoFrames;
                listSubtitles.SelectedIndex = 0;
            }
           
            m_saveXmlTimer = ThreadPoolTimer.CreatePeriodicTimer(async (success) =>
            {
                System.Collections.ObjectModel.ObservableCollection<VideoFrame> frames = p.VideoFrames;
                await G.SaveXml(p.ProjectFile, frames);
            }, TimeSpan.FromMinutes(1));

            m_folder = await p.VideoFrames[0]?.ImageFile.GetParentAsync();
        }
        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            m_saveXmlTimer.Cancel();
            m_saveXmlTimer = null;

            IReadOnlyList<StorageFile> files = await m_folder.GetFilesAsync();
            foreach (var item in files)
            {
                if (item.Name.EndsWith(".tmp"))
                    await item.DeleteAsync();
            }
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

            // Add undo
            StorageFolder folder = await frame.ImageFile.GetParentAsync();
            VideoFrame undo1 = framePrev.GetCopy();
            undo1.ImageFile = await framePrev.ImageFile.CopyAsync(folder, framePrev.ImageFile.DisplayName + ".tmp");
            VideoFrame undo2 = frame.GetCopy();
            undo2.ImageFile = await frame.ImageFile.CopyAsync(folder, frame.ImageFile.DisplayName + ".tmp");

            frame.BeginTime = framePrev.BeginTime;
            frame.Subtitle = frame.UserEdited ? frame.Subtitle : framePrev.Subtitle;
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

            UndoFrames undoFrames = new UndoFrames();
            undoFrames.OldFrames = new List<VideoFrame> { undo1, undo2 };
            undoFrames.NewFrames = new List<VideoFrame> { frame };
            undoFrames.Time = undo1.BeginTime;
            p.UndoFrames.Add(undoFrames);

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

            // Add undo
            StorageFolder folder = await frame.ImageFile.GetParentAsync();
            VideoFrame undo1 = frame.GetCopy();
            undo1.ImageFile = await frame.ImageFile.CopyAsync(folder, frame.ImageFile.DisplayName + ".tmp", NameCollisionOption.ReplaceExisting);
            VideoFrame undo2 = frameNext.GetCopy();
            undo2.ImageFile = await frameNext.ImageFile.CopyAsync(folder, frameNext.ImageFile.DisplayName + ".tmp", NameCollisionOption.ReplaceExisting);

            frame.EndTime = frameNext.EndTime;
            frame.Subtitle = frame.UserEdited ? frame.Subtitle : frameNext.Subtitle;
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
            
            if (p.UndoFrames.Count == 5)
                p.UndoFrames.RemoveAt(0);

            UndoFrames undoFrames = new UndoFrames();
            undoFrames.OldFrames = new List<VideoFrame> { undo1, undo2 };
            undoFrames.NewFrames = new List<VideoFrame> { frame };
            undoFrames.Time = undo1.BeginTime;
            p.UndoFrames.Add(undoFrames);

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

            // Add undo
            VideoFrame undo = frame.GetCopy();
            //string tempName = undo.ImageFile.DisplayName + ".tmp";
            await undo.ImageFile.RenameAsync(undo.ImageFile.DisplayName + ".tmp", NameCollisionOption.ReplaceExisting);

            if (p.UndoFrames.Count == 5)
                p.UndoFrames.RemoveAt(0);

            UndoFrames undoFrames = new UndoFrames();
            undoFrames.OldFrames = new List<VideoFrame> { undo };
            undoFrames.Time = undo.BeginTime;
            p.UndoFrames.Add(undoFrames);

            p.VideoFrames.Remove(frame);

            //await frame.ImageFile.DeleteAsync();
            EnableEditControls(panel, true);
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
            foreach (VideoFrame frame in p.VideoFrames)
            {
                // Skip OCR if user edited manually.
                if (frame.UserEdited)
                    continue;

                await OCRImage(frame);
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
            if (buttonPencilFill.IsChecked == true || buttonFloodFill.IsChecked == true)
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

            if (buttonFloodFill.IsChecked == true)
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
                bool result = Helper.FloodFill(pixels, (int)m_bitmapEdit.SizeInPixels.Width, (int)m_bitmapEdit.SizeInPixels.Height, ptFill, size, Colors.Black, Colors.White);

                Debug.WriteLine(string.Format("pt {0} size {1}", ptFill.ToString(), size));
                m_bitmapEdit.SetPixelColors(pixels);
                canvasControlEdit.Invalidate();
            }
            else if (buttonRectangleFill.IsChecked == true)
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

                int size = Convert.ToInt16((comboBoxPencilSize.SelectedItem as FrameworkElement).Tag);
                if (Helper.FloodFill(pixels, (int)m_bitmapEdit.SizeInPixels.Width, (int)m_bitmapEdit.SizeInPixels.Height, ptCanvasStart, size, Colors.Black, Colors.White))
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

        private async void buttonOCR_Click(object sender, RoutedEventArgs e)
        {
            await OCRImage(((VideoFrame)((Button)sender).DataContext));
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

        private void textBoxSubtitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            // If text changed by data binding, both Data Context and UI will have same value.
            // Otherwise UI will change first before apply back to data binding value.
            VideoFrame frame = (VideoFrame)((TextBox)sender).DataContext;
            if (frame == null)
                return;

            string dcText = frame.Subtitle;
            string uiText = ((TextBox)sender).Text;

            frame.UserEdited = dcText != uiText;


        }

        private async Task OCRImage(VideoFrame frame)
        {
            SoftwareBitmap softwareBitmap;

            using (IRandomAccessStream stream = await frame.ImageFile.OpenAsync(FileAccessMode.Read))
            {
                // Create the decoder from the stream
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                // Get the SoftwareBitmap representation of the file
                softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                var ocrResult = await m_ocrEngine.RecognizeAsync(softwareBitmap);
                frame.Subtitle = ocrResult.Text.Replace(" ", "");
            }
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

        private void textEndTime_Tapped(object sender, TappedRoutedEventArgs e)
        {
            TextBlock textEndTime = sender is TextBlock ? sender as TextBlock : G.FindControl<TextBlock>((sender as FrameworkElement).Parent, "textEndTime");
            TextBox textBoxEndTime = G.FindControl<TextBox>((sender as FrameworkElement).Parent, "textBoxEndTime");
            Button buttonEndTimeOK = sender is Button ? sender as Button : G.FindControl<Button>((sender as FrameworkElement).Parent, "buttonEndTimeOK");

            textEndTime.Visibility = textEndTime.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            textBoxEndTime.Visibility = textBoxEndTime.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            buttonEndTimeOK.Visibility = textBoxEndTime.Visibility;
        }

        private void textBeginTime_Tapped(object sender, TappedRoutedEventArgs e)
        {
            TextBlock textBeginTime = sender is TextBlock ? sender as TextBlock : G.FindControl<TextBlock>((sender as FrameworkElement).Parent, "textBeginTime");
            TextBox textBoxBeginTime = G.FindControl<TextBox>((sender as FrameworkElement).Parent, "textBoxBeginTime");
            Button buttonBeginTimeOK = sender is Button ? sender as Button : G.FindControl<Button>((sender as FrameworkElement).Parent, "buttonBeginTimeOK");
            TextBlock textBlockSep = G.FindControl<TextBlock>((sender as FrameworkElement).Parent, "textBlockSep");

            textBeginTime.Visibility = textBeginTime.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            textBoxBeginTime.Visibility = textBoxBeginTime.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            buttonBeginTimeOK.Visibility = textBoxBeginTime.Visibility;
            string rightOf = (string)textBlockSep.GetValue(RelativePanel.RightOfProperty);
            textBlockSep.SetValue(RelativePanel.RightOfProperty, rightOf == "textBeginTime" ? "buttonBeginTimeOK" : "textBeginTime");
        }

        private async void buttonFramesUndo_Click(object sender, RoutedEventArgs e)
        {
            if (p.UndoFrames.Count > 0)
            {
                int lastIndex = p.UndoFrames.Count - 1;
                for (int i = 0; i < p.VideoFrames.Count; i++)
                {
                    if (p.UndoFrames[lastIndex].Time <= p.VideoFrames[i].BeginTime)
                    {
                        foreach (var item in p.UndoFrames[lastIndex].NewFrames)
                        {
                            await item.ImageFile.DeleteAsync();
                            p.VideoFrames.Remove(item);
                        }

                        int j = i;
                        foreach (var item in p.UndoFrames[lastIndex].OldFrames)
                        { 
                            string newName = item.ImageFile.DisplayName + ".bmp";
                            await item.ImageFile.RenameAsync(newName, NameCollisionOption.ReplaceExisting);
                            p.VideoFrames.Insert(j, item);
                            j++;
                        }                       
                        break;
                    }
                }

                if (p.RedoFrames.Count == 5)
                    p.RedoFrames.RemoveAt(0);
                p.RedoFrames.Add(p.UndoFrames[lastIndex]);
                p.UndoFrames.RemoveAt(lastIndex);
            }
        }

        private void buttonFramesRedo_Click(object sender, RoutedEventArgs e)
        {

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
            TimeSpan ts;
            TimeSpan.TryParseExact(value as string, @"hh\:mm\:ss\,fff", System.Globalization.CultureInfo.CurrentCulture, out ts);
            return ts;
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
