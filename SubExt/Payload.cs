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
using Windows.UI.Core;
using SubExt.Model;

namespace SubExt.ViewModel
{
    public class Payload : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public ObservableCollection<VideoFrame> VideoFrames { get; set; }
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


        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler eventHandler = PropertyChanged;
            if (eventHandler != null)
            {
                G.UIThreadExecute(() => { eventHandler(this, new PropertyChangedEventArgs(propertyName)); });
            }
        }
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
    }
}
