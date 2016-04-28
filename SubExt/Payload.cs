using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using SubExt.Model;

namespace SubExt.ViewModel
{
    public class Payload
    {
        public ObservableCollection<VideoFrame> VideoFrames { get; set; }
        public StorageFile Video { get; set; }

        public Size OriginalSize { get; set; }
        public Rect SubtitleRect { get; set; }
        public int StampSmoothness { get; set; }
        public double StampThreshold { get; set; }
    }
}
