using System;
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
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {

        }   

        private async void buttonOpenVideo_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add("*");
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            p.Video = await openPicker.PickSingleFileAsync();
            if (p.Video != null)
                Frame.Navigate(typeof(PreviewPage), p);
        }
    }
}
