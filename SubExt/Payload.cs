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
using Windows.Storage;
using SubExt.Model;

namespace SubExt.ViewModel
{
    public class Payload : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<VideoFrame> VideoFrames { get; set; }
        public StorageFile Video { get; set; }
        public Size OriginalSize { get; set; }
        public Rect SubtitleRect
        {
            get { return _subtitleRect; }
            set { _subtitleRect = value; RaisePropertyChanged(); }
        }
        private Rect _subtitleRect;
        public int StampSmoothness { get; set; }
        public double StampThreshold { get; set; }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
