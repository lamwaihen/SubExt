using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using SubExt.Model;

namespace SubExt.ViewModel
{
    public class Payload : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public ObservableCollection<VideoFrame> VideoFrames { get; set; }
        public StorageFile ProjectFile { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public StorageFile Video { get; set; }
        public MediaRatio FrameRate { get; set; }
        public TimeSpan Duration
        {
            get { return _duration; }
            set
            {
                _duration = value;
                OnPropertyChanged();
            }
        }
        private TimeSpan _duration;
        public TimeSpan CurrentFrameTime
        {
            get { return _currentFrameTime; }
            set { _currentFrameTime = value; OnPropertyChanged(); }
        }
        private TimeSpan _currentFrameTime;
        public Size VideoSize
        {
            get { return _videoSize; }
            set { _videoSize = value; OnPropertyChanged(); }
        }
        private Size _videoSize;
        public Rect VideoPreview
        {
            get { return _videoPreview; }
            set { _videoPreview = value; OnPropertyChanged(); }
        }
        private Rect _videoPreview;
        public Rect SubtitleRect
        {
            get { return _subtitleRect; }
            set
            {
                if (_subtitleRect.Equals(value))
                    return;
                _subtitleRect = value;
                OnPropertyChanged();

                Size aspectRatio = new Size(_videoSize.Width / _videoPreview.Width, _videoSize.Height / _videoPreview.Height);
                SubtitleUIRect = new Rect(
                    _subtitleRect.X / aspectRatio.Width + _videoPreview.Left, _subtitleRect.Y / aspectRatio.Height + _videoPreview.Top,
                    _subtitleRect.Width / aspectRatio.Width, _subtitleRect.Height / aspectRatio.Height);

                // Write this rect to settings
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(_videoSize.ToString()))
                    ApplicationData.Current.LocalSettings.Values[_videoSize.ToString()] = _subtitleRect;
                else
                    ApplicationData.Current.LocalSettings.Values.Add(_videoSize.ToString(), _subtitleRect);
            }    
        }
        private Rect _subtitleRect;
        public Rect SubtitleUIRect
        {
            get { return _subtitleUIRect; }
            set
            {
                if (_subtitleUIRect.Equals(value))
                    return;
                _subtitleUIRect = value;
                OnPropertyChanged();

                Size aspectRatio = new Size(_videoSize.Width / _videoPreview.Width, _videoSize.Height / _videoPreview.Height);
                SubtitleRect = new Rect((_subtitleUIRect.Left - _videoPreview.Left) * aspectRatio.Width, (_subtitleUIRect.Top - _videoPreview.Top) * aspectRatio.Height,
                    _subtitleUIRect.Width * aspectRatio.Width, _subtitleUIRect.Height * aspectRatio.Height);
            }
        }
        private Rect _subtitleUIRect = new Rect(10, 10, 200, 50);
        public int StampSmoothness { get; set; }
        public double StampThreshold { get; set; }
        public StorageFile SelectedImageFile
        {
            get { return _selectedImageFile; }
            set { _selectedImageFile = value; OnPropertyChanged(); }
        }
        private StorageFile _selectedImageFile;
        
        public List<Color[]> RedoPixels
        {
            get { return _redoPixels; }
            set { _redoPixels = value; OnPropertyChanged(); }
        }
        private List<Color[]> _redoPixels = new List<Color[]>(5);
        public List<Color[]> UndoPixels
        {
            get { return _undoPixels; }
            set { _undoPixels = value; OnPropertyChanged(); }
        }
        private List<Color[]> _undoPixels = new List<Color[]>(5);

        public List<UndoFrames> RedoFrames
        {
            get { return _redoFrames; }
            set { _redoFrames = value;OnPropertyChanged(); }
        }
        private List<UndoFrames> _redoFrames = new List<UndoFrames>(5);
        public List<UndoFrames> UndoFrames
        {
            get { return _undoFrames; }
            set { _undoFrames = value; OnPropertyChanged(); }
        }
        private List<UndoFrames> _undoFrames = new List<UndoFrames>(5);

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler eventHandler = PropertyChanged;
            if (eventHandler != null)
            {
                G.UIThreadExecute(() => { eventHandler(this, new PropertyChangedEventArgs(propertyName)); });
            }
        }
    }

    public class UndoFrames
    {       
        public List<VideoFrame> NewFrames
        {
            get { return _newFrames; }
            set { _newFrames = value; }
        }
        private List<VideoFrame> _newFrames = new List<VideoFrame>(2);
        public List<VideoFrame> OldFrames
        {
            get { return _oldFrames; }
            set { _oldFrames = value; }
        }
        private List<VideoFrame> _oldFrames = new List<VideoFrame>(2);
        public TimeSpan Time
        {
            get { return _time; }
            set { _time = value; }
        }
        private TimeSpan _time;
    }

    public class G
    {
        //static public CoreDispatcher UIDispatcher { get; private set; }
        static public CoreDispatcher UIDispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
        static public void UIThreadExecute(Action action)
        {
            InnerExecute(action).Wait();
        }
        static private async Task InnerExecute(Action action)
        {
            if (UIDispatcher.HasThreadAccess)
                action();
            else
                await UIDispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
        }

        static private List<FrameworkElement> AllChildren(DependencyObject parent)
        {
            var _List = new List<FrameworkElement>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var _Child = VisualTreeHelper.GetChild(parent, i);
                if (_Child is FrameworkElement)
                {
                    _List.Add(_Child as FrameworkElement);
                }
                _List.AddRange(AllChildren(_Child));
            }
            return _List;
        }

        static public T FindControl<T>(DependencyObject parentContainer, string controlName)
        {
            var childControls = AllChildren(parentContainer);
            var control = childControls.OfType<FrameworkElement>().Where(x => x.Name.Equals(controlName)).Cast<T>().First();
            return control;
        }

        static public async Task SaveXml(StorageFile file, ObservableCollection<VideoFrame> frames)
        {
            if (true || file == null || frames == null)
                return;

            string folderPath = file.Path.Substring(0, file.Path.LastIndexOf("\\"));

            await FileIO.WriteTextAsync(file, "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n", Windows.Storage.Streams.UnicodeEncoding.Utf8);
            await FileIO.AppendTextAsync(file, "<SubExt Folder=\"" + folderPath + "\">", Windows.Storage.Streams.UnicodeEncoding.Utf8);
            
            string[] separators = new string[] { "-", ".bmp" };
            foreach (VideoFrame frame in frames)
            {
                // Add to project 
                string output = VideoFrame.SerializeToXML(frame);
                await FileIO.AppendTextAsync(file, output, Windows.Storage.Streams.UnicodeEncoding.Utf8);
            }
            // Close project
            await FileIO.AppendTextAsync(file, "</SubExt>", Windows.Storage.Streams.UnicodeEncoding.Utf8);
        }
    }
}
