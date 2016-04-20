using System;
using System.ComponentModel;
using Windows.Foundation;
using Windows.Storage;

namespace SubExt.Model
{
    public class VideoFrame : INotifyPropertyChanged
    {
        public TimeSpan TimeStamp { get; set; }
        private string _subtitle;
        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value; RaisePropertyChanged("Subtitle"); }
        }
        private TimeSpan _duration;
        public TimeSpan Duration
        {
            get { return _duration; }
            set { _duration = value; RaisePropertyChanged("Duration"); }
        }
        private StorageFile _image;
        public StorageFile Image
        {
            get { return _image; }
            set { _image = value; RaisePropertyChanged("Image"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
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
