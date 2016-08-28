using SubExt.Model;
using SubExt.ViewModel;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SubExt
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Payload p;
        public MainPage()
        {
            this.InitializeComponent();

            p = new Payload();                                                
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            IReadOnlyList<StorageFolder> folders = await ApplicationData.Current.TemporaryFolder.GetFoldersAsync();

            foreach (StorageFolder folder in folders)
            {
                Button buttonOpenProject = new Button
                {
                    Content = "Open " + folder.Name,
                    DataContext = folder                 
                };
                buttonOpenProject.Click += buttonOpenProjects_Click;
                panelProjects.Children.Add(buttonOpenProject);
            }
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {

        }
        
        private async void buttonOpenProjects_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder = (StorageFolder)((Button)sender).DataContext;
            p.Name = folder.Name;
            p.DisplayName = folder.Name.Substring(0, folder.Name.LastIndexOf("."));
            IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
            string[] separators = new string[] { "-", ".bmp" };
            p.VideoFrames = new System.Collections.ObjectModel.ObservableCollection<VideoFrame>();
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
            }

            Frame.Navigate(typeof(SubtitlePage), p);
        }

        private async void buttonOpenVideo_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add("*");
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            p.Video = await openPicker.PickSingleFileAsync();
            if (p.Video != null)
            {
                p.Name = p.Video.Name;
                p.DisplayName = p.Video.DisplayName;
                Frame.Navigate(typeof(PreviewPage), p);
            }
        }
    }
}
