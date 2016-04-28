using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Foundation;
using Windows.Storage;

namespace SubExt.Model
{
    public class VideoFrame : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public TimeSpan BeginTime
        {
            get { return _beginTime; }
            set { _beginTime = value; RaisePropertyChanged(); }
        }
        private TimeSpan _beginTime;
        public TimeSpan EndTime
        {
            get { return _endTime; }
            set { _endTime = value; RaisePropertyChanged(); }
        }
        private TimeSpan _endTime;
        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value; RaisePropertyChanged(); }
        }
        private string _subtitle;
        public StorageFile Image
        {
            get { return _image; }
            set { _image = value; RaisePropertyChanged(); }
        }
        private StorageFile _image;
        public Size ImageSize
        {
            get { return _imageSize; }
            set { _imageSize = value; RaisePropertyChanged(); }
        }
        private Size _imageSize;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Create a copy of a SubtitleExtractor to save.
        // If your object is databound, this copy is not databound.
        public VideoFrame GetCopy()
        {
            VideoFrame copy = (VideoFrame)this.MemberwiseClone();
            return copy;
        }
    }
}
