using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;

namespace SubExt
{
    class Payload
    {
        public StorageFile Video { get; set; }

        public Size OriginalSize { get; set; }
        public Rect SubtitleRect { get; set; }
        public int StampSmoothness { get; set; }
        public double StampThreshold { get; set; }
    }
}
